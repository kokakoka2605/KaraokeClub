CREATE DATABASE KaraokeClub
GO

USE KaraokeClub
GO

--ROLE
CREATE TABLE roles (
    id_role INT PRIMARY KEY IDENTITY(1,1),
    name_role VARCHAR(50) NOT NULL UNIQUE,
    salary DECIMAL(10,2) NOT NULL
)
GO


--Type
CREATE TABLE type (
    id_type INT PRIMARY KEY IDENTITY(1,1),
    name_type VARCHAR(50) UNIQUE NOT NULL
)
GO

--KARAOKE
CREATE TABLE karaoke (
    id_option INT PRIMARY KEY IDENTITY(1,1),
    name_option VARCHAR(255) UNIQUE NOT NULL,
    price_option DECIMAL(10,2) NOT NULL CHECK (price_option >= 0),
    descriptionn VARCHAR(MAX)
)
GO

--WORKERS
CREATE TABLE workers (
    id_worker INT PRIMARY KEY IDENTITY(1,1),
    name_worker VARCHAR(255) NOT NULL,
    id_role INT NOT NULL,
    idnp CHAR(13) UNIQUE CHECK (LEN(idnp) = 13),
    birth DATE,
    addres VARCHAR(200),
    phone VARCHAR(9),
	FOREIGN KEY (id_role) REFERENCES roles(id_role)
)
GO


--MENU
CREATE TABLE menu (
    id_product INT PRIMARY KEY IDENTITY(1,1),
    name_product VARCHAR(255) NOT NULL,
    price_product DECIMAL(10,2) NOT NULL CHECK (price_product >= 0),
    id_type INT NOT NULL FOREIGN KEY (id_type) REFERENCES type(id_type),
    section VARCHAR(10) NOT NULL CHECK (section IN ('kitchen', 'bar')),
    weight_volume VARCHAR(50) NOT NULL,
    ingredients VARCHAR(500) NOT NULL,
    cooking_time INT,
	image_path VARCHAR(500) NULL
);

CREATE INDEX idx_menu_type ON menu(id_type);
CREATE INDEX idx_menu_section ON menu(section);

--ORDERS
CREATE TABLE orders (
    id_order INT PRIMARY KEY IDENTITY(1,1),
    table_number INT NOT NULL,
    id_worker INT NOT NULL FOREIGN KEY (id_worker) REFERENCES workers(id_worker),
    order_status VARCHAR(20) NOT NULL  CHECK (order_status IN ('open', 'closed', 'cancelled')) 
        DEFAULT 'open',
    guest_count INT CHECK (guest_count > 0),
    created_at DATETIME DEFAULT GETDATE()
);

CREATE INDEX idx_orders_worker ON orders(id_worker);


--ORDER ITEMS
CREATE TABLE order_items (
    id INT PRIMARY KEY IDENTITY(1,1),
    id_order INT NOT NULL FOREIGN KEY (id_order) REFERENCES orders(id_order) ON DELETE CASCADE,
    item_type VARCHAR(20) NOT NULL CHECK (item_type IN ('product', 'karaoke')),
    id_product INT FOREIGN KEY (id_product) REFERENCES menu(id_product),
    id_option INT FOREIGN KEY (id_option) REFERENCES karaoke(id_option),
    quantity INT NOT NULL CHECK (quantity > 0),
    price_at_order DECIMAL(10,2) NOT NULL CHECK (price_at_order >= 0),
    notes VARCHAR(500),

    CHECK (
        (item_type = 'product' AND id_product IS NOT NULL AND id_option IS NULL) OR
        (item_type = 'karaoke' AND id_option IS NOT NULL AND id_product IS NULL)
    )
);

CREATE INDEX idx_order_items_order ON order_items(id_order);

--BILL
CREATE TABLE bill (
    id_bill INT PRIMARY KEY IDENTITY(1,1),
    id_order INT NOT NULL UNIQUE,
    created_at DATETIME DEFAULT GETDATE(),
    total_amount DECIMAL(10,2),
    deposit DECIMAL(10,2),
    payment_method VARCHAR(20) 
        CHECK (payment_method IN ('cash', 'card', 'online')),
    bill_status VARCHAR(20) DEFAULT 'unpaid'
        CHECK (bill_status IN ('unpaid', 'paid', 'partial')),
    FOREIGN KEY (id_order) REFERENCES orders(id_order) ON DELETE CASCADE
);

-- APP_USERS
CREATE TABLE app_users (
    id_user   INT PRIMARY KEY IDENTITY(1,1),
    username  VARCHAR(50)  NOT NULL UNIQUE,
    password  VARCHAR(255) NOT NULL,
    app_role  VARCHAR(10)  NOT NULL CHECK (app_role IN ('admin', 'user')),
	id_worker INT FOREIGN KEY REFERENCES workers(id_worker)
)
GO



--1. Автоподсчёт bill.total_amount
GO
CREATE TRIGGER trg_update_bill_total
ON order_items
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE b
    SET total_amount = ISNULL((
        SELECT SUM(oi.quantity * oi.price_at_order)
        FROM order_items oi
        WHERE oi.id_order = b.id_order
    ), 0)
    FROM bill b
    WHERE b.id_order IN (
        SELECT id_order FROM inserted
        UNION
        SELECT id_order FROM deleted
    );
END;
GO

--2. Автоподстановка price_at_order
GO
CREATE OR ALTER TRIGGER trg_order_items_insert
ON order_items
INSTEAD OF INSERT
AS
BEGIN
    SET NOCOUNT ON;

    -- Проверка закрытого заказа
    IF EXISTS (
        SELECT 1
        FROM orders o
        JOIN inserted i ON o.id_order = i.id_order
        WHERE o.order_status = 'closed'
    )
    BEGIN
        RAISERROR('Нельзя добавлять в закрытый заказ', 16, 1);
        RETURN;
    END

    -- Вставка с автоподстановкой цены
    INSERT INTO order_items (
        id_order,
        item_type,
        id_product,
        id_option,
        quantity,
        price_at_order,
        notes
    )
    SELECT 
        i.id_order,
        i.item_type,
        i.id_product,
        i.id_option,
        i.quantity,
        CASE 
            WHEN i.item_type = 'product' THEN m.price_product
            WHEN i.item_type = 'karaoke' THEN k.price_option
        END,
        i.notes
    FROM inserted i
    LEFT JOIN menu m ON i.id_product = m.id_product
    LEFT JOIN karaoke k ON i.id_option = k.id_option;
END;
GO

--Автосоздание счета (bill)
CREATE TRIGGER trg_create_bill
ON orders
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO bill (id_order, total_amount, bill_status)
    SELECT id_order, 0, 'unpaid'
    FROM inserted;
END;
GO





--ROLE
INSERT INTO roles (name_role, salary)
VALUES
('Waiter', 5000),
('Cook', 7000),
('Bartender', 6500),
('Admin', 8000);


--TYPE
INSERT INTO type (name_type) VALUES
('алкогольное'),
('безалкогольное'),
('горячее'),
('закуски'),
('салаты'),
('десерты'),
('соусы'),
('горячие напитки');


--KARAOKE
INSERT INTO karaoke (name_option, price_option, descriptionn)
VALUES
('1 Song', 50.00, 'исполнение одной песни'),
('5 Songs Pack', 200.00, 'пакет из 5 песен'),
('VIP Hour', 500.00, 'час караоке без ограничений');


--WORKERS
INSERT INTO workers (name_worker, id_role, idnp, birth, addres, phone)
VALUES
('Ivan Ivanov', 1, '1000000000001', '1995-05-10', 'Chisinau', '069000001'),
('Maria Popescu', 2, '2000000000001', '1990-03-22', 'Chisinau', '069000002'),
('John Smith', 3, '3000000000001', '1992-07-15', 'Chisinau', '069000003'),
('Ivanoglo Tatiana', 4, '4000000000001','2005-01-26', 'Chisinau','06998547');



--MENU
INSERT INTO menu (name_product, price_product, id_type, section, weight_volume, ingredients, cooking_time)
VALUES

-- КОКТЕЙЛИ (Алкогольные)
('Секс на пляже', 80.00, 1, 'bar', '200мл', 'Тропический коктейль с водкой, персиковым ликёром и фруктовыми соками', NULL),
('Голубая Лагуна', 65.00, 1, 'bar', '200мл', 'Яркий тропический коктейль с водкой и синим сиропом', NULL),
('PARK-ZONE', 90.00, 1, 'bar', '250мл', 'Эксклюзивный фирменный коктейль с экзотическими ингредиентами', NULL),
('Фламинго', 80.00, 1, 'bar', '200мл', 'Яркий тропический коктейль с ромом и фруктовыми соками', NULL),
('Espresso Martini', 75.00, 1, 'bar', '150мл', 'Стильный кофейный коктейль с водкой и эспрессо', NULL),
('Cosmopolitan', 85.00, 1, 'bar', '200мл', 'Элегантный классический коктейль с цитрусовой водкой и клюквой', NULL),
('Порн стар мартини', 100.00, 1, 'bar', '200мл', 'Сладкий тропический коктейль с маракуйей и ванилью', NULL),
('Маргарита', 60.00, 1, 'bar', '120мл', 'Классический мексиканский коктейль с текилой и лаймом', NULL),
('Виски Соур', 70.00, 1, 'bar', '120мл', 'Классический кислый коктейль на основе виски', NULL),
('В-52', 70.00, 1, 'bar', '150мл', 'Элегантный многослойный коктейль с кофейным и сливочным ликёром', NULL),
('Писанг Амбон', 70.00, 1, 'bar', '150мл', 'Экзотический коктейль с абсентом и текилой', NULL),
('Хиросима', 70.00, 1, 'bar', '150мл', 'Крепкий многослойный коктейль', NULL),

-- МОКТЕЙЛИ (Безалкогольные)
('Голубая Лагуна (безалк.)', 55.00, 2, 'bar', '200мл', 'Безалкогольная версия яркого тропического коктейля', NULL),
('Мохито (безалк.)', 60.00, 2, 'bar', '250мл', 'Освежающий безалкогольный мохито с мятой и лаймом', NULL),
('Сейдж Романс', 60.00, 2, 'bar', '200мл', 'Нежный фруктовый безалкогольный коктейль с киви', NULL),
('Грейпфрутовый Фруточино', 60.00, 2, 'bar', '250мл', 'Освежающий безалкогольный коктейль с грейпфрутом и кофе', NULL),
('Coca-Cola 0.33л', 20.00, 2, 'bar', '330мл', 'Классическая газированная вода Coca-Cola', NULL),
('Coca-Cola Zero 0.33л', 20.00, 2, 'bar', '330мл', 'Газированная вода без сахара', NULL),
('Fanta 0.33л', 20.00, 2, 'bar', '330мл', 'Освежающая апельсиновая газированная вода', NULL),
('Sprite 0.33л', 20.00, 2, 'bar', '330мл', 'Освежающая лимонно-лаймовая газированная вода', NULL),
('Coca-Cola 0.25л', 20.00, 2, 'bar', '250мл', 'Классическая газированная вода Coca-Cola', NULL),
('Coca-Cola Zero 0.25л', 20.00, 2, 'bar', '250мл', 'Газированная вода без сахара', NULL),
('Fanta 0.25л', 20.00, 2, 'bar', '250мл', 'Апельсиновая газированная вода', NULL),
('Sprite 0.25л', 20.00, 2, 'bar', '250мл', 'Лимонно-лаймовая газированная вода', NULL),
('Schweppes Pomegranate 0.33л', 25.00, 2, 'bar', '330мл', 'Гранатовый тоник', NULL),
('Schweppes Bitter Lemon 0.33л', 25.00, 2, 'bar', '330мл', 'Лимонный тоник', NULL),
('Schweppes Purple Tonic 0.33л', 25.00, 2, 'bar', '330мл', 'Пурпурный тоник', NULL),
('Schweppes Pink Tonic 0.33л', 25.00, 2, 'bar', '330мл', 'Розовый тоник', NULL),

-- БУРГЕРЫ (горячее)
('Бургер куриный', 110.00, 3, 'kitchen', '260г', 'Куриная котлета с айсбергом, помидором, солёным огурцом', 15),
('Бургер говяжий', 120.00, 3, 'kitchen', '320г', 'Говяжья котлета с сыром, соусом, солёным огурцом и маринованными овощами', 15),
('URBAN GRILL', 169.00, 3, 'kitchen', '455г', 'Свинно-куриные гриль-сосиски. Отборная свинина', 20),
('Urban Chicken Grill', 159.00, 3, 'kitchen', '455г', 'Гриль-сосиски из курицы с картофелем на шпажке, жареным луком', 20),
('Паста с грибами', 115.00, 3, 'kitchen', '320г', 'Классическая паста с шампиньонами в сливочном соусе', 15),
('Паста с курицей и грибами', 130.00, 3, 'kitchen', '320г', 'Нежная паста с курицей сувид и шампиньонами в сливочном соусе', 15),
('Паста с сыром', 110.00, 3, 'kitchen', '370г', 'Сытная паста в ароматном сырном соусе с куриным бульоном', 15),

-- ЗАКУСКИ
('Креветки Пани', 140.00, 4, 'kitchen', '250г', 'Нежные креветки в хрустящей панировке панко с соусом сладкий чили', 12),
('Филе Пани', 120.00, 4, 'kitchen', '280г', 'Сочное куриное филе в хрустящей панировке панко с соусом сладкий чили', 12),
('Филе Пани острые', 120.00, 4, 'kitchen', '280г', 'Острое куриное филе в хрустящей панировке с перцем и соусом', 12),
('Крылья в горчичном соусе', 120.00, 4, 'kitchen', '280г', 'Сочные куриные крылья в ароматном горчичном соусе', 15),
('Мясное Плато Макси Асорти', 359.00, 4, 'kitchen', '1100г', 'Мясное плато: куриные сосиски 300гр, свинно-куриные сосиски', 20),
('Сырная Тарелка', 190.00, 4, 'kitchen', '280г', 'Ассорти благородных сыров с мёдом, крекерами и грецкими орехами', 10),
('Колбасная нарезка', 210.00, 4, 'kitchen', '350г', 'Премиальные сырокопчёные колбасные изделия высших сортов', 10),
('Морской сет PARK-ZONE', 750.00, 4, 'kitchen', '1100г', 'Ароматный, щедрый и очень эффектный сет для компании', 20),
('Фисташки', 90.00, 4, 'kitchen', '100г', 'Обжаренные фисташки с морской солью', NULL),
('Орешки микс', 50.00, 4, 'kitchen', '140г', 'Ассорти обжаренных орехов со специями', NULL),
('Чипсы Chio', 80.00, 4, 'kitchen', '125г', 'Картофельные чипсы Chio (Paprika)', NULL),
('Сухарики ассорти', 30.00, 4, 'kitchen', '120г', 'Хрустящие ржаные и пшеничные сухарики с ароматными специями', NULL),
('Крекеры солёные', 20.00, 4, 'kitchen', '100г', 'Хрустящие солёные крекеры', NULL),
('Чипсы куриные', 80.00, 4, 'kitchen', '100г', 'Хрустящие ломтики курицы с пряными специями', NULL),
('Бастурма', 80.00, 4, 'kitchen', '50г', 'Тонко нарезанная бастурма из говядины', NULL),
('Гренки чесночные', 50.00, 4, 'kitchen', '150г', 'Бородинские гренки с чесночным ароматом и тёплым сырным дипом', 10),
('Фруктовая нарезка', 90.00, 4, 'kitchen', '300г', 'Свежее ассорти сезонных фруктов', 10),
('Картошка Фри', 60.00, 4, 'kitchen', '230г', 'Золотистая хрустящая картошка', 10),
('Картошка по-деревенски', 60.00, 4, 'kitchen', '230г', 'Ароматная картошка с кожурой и специями', 15),

-- САЛАТЫ
('Цезарь с креветкой', 120.00, 5, 'kitchen', '280г', 'Классический салат Цезарь с креветками, хрустящими гренками', 10),
('Цезарь с курицей', 110.00, 5, 'kitchen', '300г', 'Салат Цезарь с курицей сувид, свежими овощами, гренками', 10),
('Тёплый с говядиной', 140.00, 5, 'kitchen', '380г', 'Тёплый салат с отварной говядиной, овощами гриль', 15),

-- СОУСЫ
('Кисло-сладкий Чили', 10.00, 7, 'kitchen', '40г', 'Острый кисло-сладкий соус чили', NULL),
('Кетчуп', 10.00, 7, 'kitchen', '40г', 'Классический томатный кетчуп с натуральными томатами', NULL),
('Майонез', 10.00, 7, 'kitchen', '40г', 'Домашний классический майонез', NULL),
('Сырный', 15.00, 7, 'kitchen', '40г', 'Нежный сырный соус к горячим блюдам и закускам', NULL),
('По сезону Аджика', 10.00, 7, 'kitchen', '40г', 'Острый традиционный соус аджика (по сезону)', NULL),

-- ДЕСЕРТЫ
('Клюква-Апельсин', 85.00, 6, 'kitchen', '120г', 'Лёгкое воздушное пирожное с сочной начинкой из клюквы и апельсина', NULL),
('Сникерс', 90.00, 6, 'kitchen', '130г', 'Нежный десерт с насыщенным вкусом карамели, арахиса и шоколада', NULL),
('Кофе-Карамель-Фундук', 85.00, 6, 'kitchen', '130г', 'Ароматное пирожное с насыщенным кофейным вкусом', NULL),
('Медовик', 65.00, 6, 'kitchen', '120г', 'Классический торт с мягкими медовыми коржами и нежным кремом', NULL),
('Медовик шоколадный', 65.00, 6, 'kitchen', '120г', 'Шоколадная версия классического медовика с какао в коржах', NULL),
('Шоколадный маракуйя', 65.00, 6, 'kitchen', '120г', 'Изысканный десерт, сочетающий насыщенный вкус шоколада с ярким вкусом маракуйи', NULL),
('Тирамису', 55.00, 6, 'kitchen', '120г', 'Классический итальянский десерт с печеньем савоярди, пропитанным кофе', NULL),
('Карамельный Флан PARK-ZONE', 120.00, 6, 'kitchen', '250г', 'Нежный сливочный флан из сгущённого и свежего молока с карамелью', NULL),

-- ГОРЯЧИЕ НАПИТКИ 
('Имбирно-цитрусовый зелёный чай', 55.00, 8, 'kitchen', '1000мл', 'Пряный чай с имбирём, цитрусами, мёдом и корицей', 5),
('Зелёно-цитрусовый чай', 45.00, 8, 'kitchen', '1000мл', 'Освежающий зелёный чай с цитрусовыми нотами', 5),
('English Breakfast', 45.00, 8, 'kitchen', '500мл', 'Классический чёрный чай English Breakfast', 5),
('Американо', 20.00, 8, 'kitchen', '200мл', 'Классический чёрный кофе на основе эспрессо', 5),
('Американо с молоком', 20.00, 8, 'kitchen', '200мл', 'Американо с добавлением свежего молока', 5),
('Эспрессо', 20.00, 8, 'kitchen', '60мл', 'Насыщенный эспрессо из свежемолотого кофе', 5),
('Капучино', 25.00, 8, 'kitchen', '250мл', 'Эспрессо с бархатистой молочной пеной', 5),
('Латте', 30.00, 8, 'kitchen', '300мл', 'Мягкий латте с насыщенным вкусом кофе', 5);





--=====================================
-- ROLES
--======================================
CREATE PROCEDURE usp_Insert_Role
    @name_role VARCHAR(50),
    @salary    DECIMAL(10,2)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;
    BEGIN TRY
        INSERT INTO roles (name_role, salary) VALUES (@name_role, @salary);
        SELECT SCOPE_IDENTITY() AS NewId;  -- вернуть новый id
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

CREATE PROCEDURE usp_Update_Role
    @id_role   INT,
    @name_role VARCHAR(50),
    @salary    DECIMAL(10,2)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;
    BEGIN TRY
        UPDATE roles SET name_role = @name_role, salary = @salary
        WHERE id_role = @id_role;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

CREATE PROCEDURE usp_Delete_Role
    @id_role INT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;
    BEGIN TRY
        DELETE FROM roles WHERE id_role = @id_role;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO
--===========================
-- TYPE
--============================
CREATE PROCEDURE usp_Insert_Type
    @name_type VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRAN;
    BEGIN TRY
        INSERT INTO type (name_type) VALUES (@name_type);
        SELECT SCOPE_IDENTITY() AS NewId;
        COMMIT;
    END TRY
    BEGIN CATCH
        ROLLBACK;
        THROW;
    END CATCH
END;
GO

CREATE PROCEDURE usp_Update_Type
    @id_type INT,
    @name_type VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRAN;
    BEGIN TRY
        UPDATE type SET name_type = @name_type
        WHERE id_type = @id_type;
        COMMIT;
    END TRY
    BEGIN CATCH
        ROLLBACK;
        THROW;
    END CATCH
END;
GO

CREATE OR ALTER PROCEDURE usp_Delete_Type
    @id_type INT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRAN;

    BEGIN TRY

        -- Проверка: используется ли тип в menu
        IF EXISTS (
            SELECT 1
            FROM menu
            WHERE id_type = @id_type
        )
        BEGIN
            THROW 50007, 'Нельзя удалить тип, так как он используется в menu', 1;
        END

        DELETE FROM type
        WHERE id_type = @id_type;

        COMMIT;
    END TRY
    BEGIN CATCH
        ROLLBACK;
        THROW;
    END CATCH
END;
GO

--===============================
-- KARAOKE
--================================
CREATE PROCEDURE usp_Insert_Karaoke
    @name_option VARCHAR(255),
    @price_option DECIMAL(10,2),
    @descriptionn VARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRAN;
    BEGIN TRY
        INSERT INTO karaoke (name_option, price_option, descriptionn)
        VALUES (@name_option, @price_option, @descriptionn);

        SELECT SCOPE_IDENTITY() AS NewId;
        COMMIT;
    END TRY
    BEGIN CATCH
        ROLLBACK;
        THROW;
    END CATCH
END;
GO

CREATE PROCEDURE usp_Update_Karaoke
    @id_option INT,
    @name_option VARCHAR(255),
    @price_option DECIMAL(10,2),
    @descriptionn VARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRAN;
    BEGIN TRY
        UPDATE karaoke
        SET name_option = @name_option,
            price_option = @price_option,
            descriptionn = @descriptionn
        WHERE id_option = @id_option;
        COMMIT;
    END TRY
    BEGIN CATCH
        ROLLBACK;
        THROW;
    END CATCH
END;
GO

CREATE PROCEDURE usp_Delete_Karaoke
    @id_option INT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRAN;
    BEGIN TRY
        DELETE FROM karaoke WHERE id_option = @id_option;
        COMMIT;
    END TRY
    BEGIN CATCH
        ROLLBACK;
        THROW;
    END CATCH
END;
GO


--===============================
-- WORKERS
--===============================
CREATE PROCEDURE usp_Insert_Worker
    @name_worker VARCHAR(255),
    @id_role INT,
    @idnp CHAR(13),
    @birth DATE,
    @addres VARCHAR(200),
    @phone VARCHAR(9)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRAN;
    BEGIN TRY
        INSERT INTO workers (name_worker, id_role, idnp, birth, addres, phone)
        VALUES (@name_worker, @id_role, @idnp, @birth, @addres, @phone);

        SELECT SCOPE_IDENTITY() AS NewId;
        COMMIT;
    END TRY
    BEGIN CATCH
        ROLLBACK;
        THROW;
    END CATCH
END;
GO

CREATE PROCEDURE usp_Update_Worker
    @id_worker INT,
    @name_worker VARCHAR(255),
    @id_role INT,
    @idnp CHAR(13),
    @birth DATE,
    @addres VARCHAR(200),
    @phone VARCHAR(9)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRAN;
    BEGIN TRY
        UPDATE workers
        SET name_worker = @name_worker,
            id_role = @id_role,
            idnp = @idnp,
            birth = @birth,
            addres = @addres,
            phone = @phone
        WHERE id_worker = @id_worker;
        COMMIT;
    END TRY
    BEGIN CATCH
        ROLLBACK;
        THROW;
    END CATCH
END;
GO

CREATE PROCEDURE usp_Delete_Worker
    @id_worker INT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRAN;
    BEGIN TRY
        DELETE FROM workers WHERE id_worker = @id_worker;
        COMMIT;
    END TRY
    BEGIN CATCH
        ROLLBACK;
        THROW;
    END CATCH
END;
GO

--================================
-- MENU
--================================

CREATE OR ALTER PROCEDURE usp_Insert_Menu
    @name_product  VARCHAR(255),
    @price_product DECIMAL(10,2),
    @id_type       INT,
    @section       VARCHAR(10),
    @weight_volume VARCHAR(50),
    @ingredients   VARCHAR(500),
    @cooking_time  INT = NULL,
    @image_path    VARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRAN;
    BEGIN TRY
        INSERT INTO menu (
            name_product, price_product, id_type, section,
            weight_volume, ingredients, cooking_time, image_path
        )
        VALUES (
            @name_product, @price_product, @id_type, @section,
            @weight_volume, @ingredients, @cooking_time, @image_path
        );
        SELECT SCOPE_IDENTITY() AS NewId;
        COMMIT;
    END TRY
    BEGIN CATCH
        ROLLBACK;
        THROW;
    END CATCH
END;
GO

CREATE OR ALTER PROCEDURE usp_Update_Menu
    @id_product    INT,
    @name_product  VARCHAR(255),
    @price_product DECIMAL(10,2),
    @id_type       INT,
    @section       VARCHAR(10),
    @weight_volume VARCHAR(50),
    @ingredients   VARCHAR(500),
    @cooking_time  INT = NULL,
    @image_path    VARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRAN;
    BEGIN TRY
        UPDATE menu
        SET name_product  = @name_product,
            price_product = @price_product,
            id_type       = @id_type,
            section       = @section,
            weight_volume = @weight_volume,
            ingredients   = @ingredients,
            cooking_time  = @cooking_time,
            image_path    = @image_path
        WHERE id_product = @id_product;
        COMMIT;
    END TRY
    BEGIN CATCH
        ROLLBACK;
        THROW;
    END CATCH
END;
GO

CREATE OR ALTER PROCEDURE usp_Delete_Menu
    @id_product INT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRAN;

    BEGIN TRY

        -- Проверка: используется ли товар в заказах
        IF EXISTS (
            SELECT 1
            FROM order_items
            WHERE id_product = @id_product
        )
        BEGIN
            THROW 50003, 'Нельзя удалить товар, который уже есть в заказах', 1;
        END

        DELETE FROM menu
        WHERE id_product = @id_product;

        COMMIT;
    END TRY
    BEGIN CATCH
        ROLLBACK;
        THROW;
    END CATCH
END;
GO

--================================
--ORDER
--================================
CREATE PROCEDURE usp_Close_Order
    @id_order INT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRAN;
    BEGIN TRY
        UPDATE orders
        SET order_status = 'closed'
        WHERE id_order = @id_order;
        COMMIT;
    END TRY
    BEGIN CATCH
        ROLLBACK;
        THROW;
    END CATCH
END;
GO

CREATE PROCEDURE usp_Insert_Order
    @table_number INT,
    @id_worker INT,
    @guest_count INT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRAN;
    BEGIN TRY
        INSERT INTO orders (table_number, id_worker, guest_count)
        VALUES (@table_number, @id_worker, @guest_count);

        SELECT SCOPE_IDENTITY() AS NewId;
        -- bill создастся триггером

        COMMIT;
    END TRY
    BEGIN CATCH
        ROLLBACK;
        THROW;
    END CATCH
END;
GO



CREATE PROCEDURE usp_Update_Order
    @id_order INT,
    @table_number INT,
    @id_worker INT,
    @guest_count INT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRAN;
    BEGIN TRY

        -- ❗ Проверка
        IF EXISTS (
            SELECT 1 FROM orders
            WHERE id_order = @id_order AND order_status = 'closed'
        )
        BEGIN
            THROW 50001, 'Нельзя изменить закрытый заказ', 1;
        END

        UPDATE orders
        SET table_number = @table_number,
            id_worker = @id_worker,
            guest_count = @guest_count
        WHERE id_order = @id_order;

        COMMIT;
    END TRY
    BEGIN CATCH
        ROLLBACK;
        THROW;
    END CATCH
END;
GO

CREATE PROCEDURE usp_Delete_Order
    @id_order INT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRAN;
    BEGIN TRY

        -- ❗ Проверка
        IF EXISTS (
            SELECT 1 FROM orders
            WHERE id_order = @id_order AND order_status = 'closed'
        )
        BEGIN
            THROW 50002, 'Нельзя удалить закрытый заказ', 1;
        END

        DELETE FROM orders
        WHERE id_order = @id_order;

        -- order_items удалятся по CASCADE
        -- bill удалится по CASCADE

        COMMIT;
    END TRY
    BEGIN CATCH
        ROLLBACK;
        THROW;
    END CATCH
END;
GO



--========================
--ORDER ITEMS
--========================
CREATE PROCEDURE usp_Insert_OrderItem
    @id_order INT,
    @item_type VARCHAR(20),
    @id_product INT = NULL,
    @id_option INT = NULL,
    @quantity INT,
    @notes VARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRAN;
    BEGIN TRY
        INSERT INTO order_items (
            id_order, item_type, id_product, id_option, quantity, notes
        )
        VALUES (
            @id_order, @item_type, @id_product, @id_option, @quantity, @notes
        );
        -- price_at_order заполнится триггером
        COMMIT;
    END TRY
    BEGIN CATCH
        ROLLBACK;
        THROW;
    END CATCH
END;
GO


CREATE OR ALTER PROCEDURE usp_Update_OrderItem
    @id INT,
    @quantity INT,
    @notes VARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRAN;

    BEGIN TRY

        -- Проверка: нельзя изменять позицию в закрытом заказе
        IF EXISTS (
            SELECT 1
            FROM order_items oi
            JOIN orders o ON oi.id_order = o.id_order
            WHERE oi.id = @id
              AND o.order_status = 'closed'
        )
        BEGIN
            THROW 50005, 'Нельзя изменять позицию в закрытом заказе', 1;
        END

        -- Проверка количества
        IF @quantity <= 0
        BEGIN
            THROW 50006, 'Количество должно быть больше 0', 1;
        END

        -- Обновление позиции
        UPDATE order_items
        SET quantity = @quantity,
            notes = @notes
        WHERE id = @id;

        COMMIT;
    END TRY
    BEGIN CATCH
        ROLLBACK;
        THROW;
    END CATCH
END;
GO

CREATE OR ALTER PROCEDURE usp_Delete_OrderItem
    @id INT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRAN;

    BEGIN TRY

        -- Проверка: нельзя удалять из закрытого заказа
        IF EXISTS (
            SELECT 1
            FROM order_items oi
            JOIN orders o ON oi.id_order = o.id_order
            WHERE oi.id = @id
              AND o.order_status = 'closed'
        )
        BEGIN
            THROW 50004, 'Нельзя удалить позицию из закрытого заказа', 1;
        END

        -- Удаление позиции
        DELETE FROM order_items
        WHERE id = @id;

        COMMIT;
    END TRY
    BEGIN CATCH
        ROLLBACK;
        THROW;
    END CATCH
END;
GO
--===============================
--BILL
--===============================
CREATE PROCEDURE usp_Update_Bill
    @id_order INT,
    @payment_method VARCHAR(20),
    @bill_status VARCHAR(20),
    @deposit DECIMAL(10,2) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRAN;
    BEGIN TRY
        UPDATE bill
        SET payment_method = @payment_method,
            bill_status = @bill_status,
            deposit = @deposit
        WHERE id_order = @id_order;
        COMMIT;
    END TRY
    BEGIN CATCH
        ROLLBACK;
        THROW;
    END CATCH
END;
GO



--===================
-- APP_USERS
--======================
CREATE PROCEDURE usp_Insert_AppUser
    @username  VARCHAR(50),
    @password  VARCHAR(255),
    @app_role  VARCHAR(10),
    @id_worker INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRAN;
    BEGIN TRY
        INSERT INTO app_users (username, password, app_role, id_worker)
        VALUES (@username, @password, @app_role, @id_worker);

        SELECT SCOPE_IDENTITY() AS NewId;
        COMMIT;
    END TRY
    BEGIN CATCH
        ROLLBACK;
        THROW;
    END CATCH
END;
GO

CREATE PROCEDURE usp_Update_AppUser
    @id_user   INT,
    @username  VARCHAR(50),
    @password  VARCHAR(255),
    @app_role  VARCHAR(10),
    @id_worker INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRAN;
    BEGIN TRY
        UPDATE app_users
        SET username  = @username,
            password  = @password,
            app_role  = @app_role,
            id_worker = @id_worker
        WHERE id_user = @id_user;
        COMMIT;
    END TRY
    BEGIN CATCH
        ROLLBACK;
        THROW;
    END CATCH
END;
GO

CREATE PROCEDURE usp_Delete_AppUser
    @id_user INT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRAN;
    BEGIN TRY
        DELETE FROM app_users
        WHERE id_user = @id_user;
        COMMIT;
    END TRY
    BEGIN CATCH
        ROLLBACK;
        THROW;
    END CATCH
END;
GO


	INSERT INTO app_users (username, password, app_role, id_worker)
VALUES 
    ('admin1', 'admin1', 'admin', NULL);


-- ──────────────────────────────────────────────────────────
--  Резервное копирование
-- ──────────────────────────────────────────────────────────
GO
CREATE OR ALTER PROCEDURE usp_Backup_Database
    @BackupPath NVARCHAR(500)   -- полный путь к .bak файлу
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @sql NVARCHAR(700) =
        N'BACKUP DATABASE [KaraokeClub] TO DISK = ' + QUOTENAME(@BackupPath, '''') +
        N' WITH FORMAT, INIT, NAME = N''KaraokeClub Full Backup'';';
    EXEC sp_executesql @sql;
END
GO

-- ──────────────────────────────────────────────────────────
--  Восстановление
-- ──────────────────────────────────────────────────────────
USE master
GO

CREATE OR ALTER PROCEDURE usp_Restore_Database
    @BackupPath NVARCHAR(500)
AS
BEGIN
    SET NOCOUNT ON;

    ALTER DATABASE [KaraokeClub] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;

    DECLARE @sql NVARCHAR(700) =
        N'RESTORE DATABASE [KaraokeClub] FROM DISK = ' + QUOTENAME(@BackupPath, '''') +
        N' WITH REPLACE, RECOVERY;';
    EXEC sp_executesql @sql;

    ALTER DATABASE [KaraokeClub] SET MULTI_USER;
END
GO
USE KaraokeClub;
GO
USE KaraokeClub;
GO

DBCC CHECKIDENT ('orders', RESEED, 20);
GO
DBCC CHECKIDENT ('bill', RESEED, 20);
DBCC CHECKIDENT ('order_items', RESEED, 60);
GO
SELECT MAX(id) FROM order_items;