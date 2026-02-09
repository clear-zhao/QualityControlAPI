DROP DATABASE IF EXISTS QualityControl;
CREATE DATABASE QualityControl DEFAULT CHARACTER SET utf8mb4;
USE QualityControl;

-- 1. 用户表
CREATE TABLE Users (
  Id INT AUTO_INCREMENT PRIMARY KEY,
  Name VARCHAR(50) NOT NULL,
  EmployeeId VARCHAR(50) NOT NULL,
  Password VARCHAR(255) NOT NULL,
  Role TINYINT NOT NULL DEFAULT 0,
  CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UNIQUE KEY UK_Users_EmployeeId (EmployeeId)
);

-- 2. 压接工具
CREATE TABLE CrimpingTools (
  Id INT AUTO_INCREMENT PRIMARY KEY,
  Model VARCHAR(100) NOT NULL,
  Type VARCHAR(20) NOT NULL,
  UNIQUE KEY UK_CrimpingTools_Model (Model)
);

-- 3. 端子规格
CREATE TABLE TerminalSpecs (
  Id INT AUTO_INCREMENT PRIMARY KEY,
  MaterialCode VARCHAR(50) NOT NULL,
  Name VARCHAR(100) NOT NULL,
  Description VARCHAR(255) NULL,
  Method TINYINT NOT NULL,
  UNIQUE KEY UK_TerminalSpecs_MaterialCode (MaterialCode)
);

-- 4. 导线规格
CREATE TABLE WireSpecs (
  Id VARCHAR(20) NOT NULL PRIMARY KEY,
  DisplayName VARCHAR(100) NOT NULL,
  SectionArea DECIMAL(6,2) NOT NULL
);

-- 5. 拉力标准
CREATE TABLE PullForceStandards (
  Id INT AUTO_INCREMENT PRIMARY KEY,
  Method TINYINT NOT NULL,
  SectionArea DECIMAL(6,2) NOT NULL,
  StandardValue INT NOT NULL
);

-- 6. 生产订单 (关键修改：完全匹配前端字段，ID改为字符串)
CREATE TABLE ProductionOrders (
  Id VARCHAR(64) NOT NULL PRIMARY KEY, -- 改为字符串，匹配前端生成的 PO-xxx
  ProductionOrderNo VARCHAR(64) NOT NULL,
  ProductName VARCHAR(128) NULL,
  ProductModel VARCHAR(128) NULL, -- 新增
  ToolNo VARCHAR(50) NULL,        -- 新增
  TerminalSpecId VARCHAR(50) NULL, -- 新增
  WireSpecId VARCHAR(50) NULL,     -- 新增
  StandardPullForce DECIMAL(10,2) NULL, -- 新增
  CreatorName VARCHAR(50) NULL,    -- 新增
  CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- 7. 检验记录
CREATE TABLE InspectionRecords (
  Id VARCHAR(64) NOT NULL PRIMARY KEY, -- 改为字符串，匹配前端 Guid
  OrderId VARCHAR(64) NULL,            -- 对应订单主键
  Type VARCHAR(20) NULL,               -- 首件/末件
  SubmitterName VARCHAR(50) NULL,
  SubmittedAt DATETIME NULL,
  Status INT NOT NULL DEFAULT 0,       -- 0=待审, 1=合格, 2=不合格
  AuditorName VARCHAR(50) NULL,
  AuditedAt DATETIME NULL,
  AuditNote VARCHAR(255) NULL,
  CONSTRAINT FK_Records_Order FOREIGN KEY (OrderId) REFERENCES ProductionOrders(Id) ON DELETE CASCADE
);

-- 8. 样本数据
CREATE TABLE TerminalSamples (
  Id INT AUTO_INCREMENT PRIMARY KEY,
  InspectionRecordId VARCHAR(64) NULL,
  SampleIndex INT NOT NULL,
  MeasuredForce DECIMAL(10,2) NULL,
  IsPassed TINYINT NULL,
  CONSTRAINT FK_Samples_Record FOREIGN KEY (InspectionRecordId) REFERENCES InspectionRecords(Id) ON DELETE CASCADE
);

-- 插入一些基础数据 (防止前端拉不到列表报错)
INSERT INTO Users (Name, EmployeeId, Password, Role) VALUES ('管理员', 'admin', '123', 1), ('张工', '1001', '123', 0);
INSERT INTO CrimpingTools (Model, Type) VALUES ('Tool-A', '手动'), ('Tool-B', '机器');
INSERT INTO TerminalSpecs (MaterialCode, Name, Description, Method) VALUES ('2000473580', '示例端子A', '描述...', 0);
INSERT INTO WireSpecs (Id, DisplayName, SectionArea) VALUES ('W-0.5', '0.5mm²', 0.5);
INSERT INTO PullForceStandards (Method, SectionArea, StandardValue) VALUES (0, 0.5, 85);

show tables ;

USE QualityControl;

-- =============================================
-- 1. 清理旧数据 (如果需要重置，请取消注释以下行)
-- =============================================
-- SET FOREIGN_KEY_CHECKS = 0;
-- TRUNCATE TABLE TerminalSamples;
-- TRUNCATE TABLE InspectionRecords;
-- TRUNCATE TABLE ProductionOrders;
-- TRUNCATE TABLE PullForceStandards;
-- TRUNCATE TABLE WireSpecs;
-- TRUNCATE TABLE TerminalSpecs;
-- TRUNCATE TABLE CrimpingTools;
-- TRUNCATE TABLE Users;
-- SET FOREIGN_KEY_CHECKS = 1;

-- =============================================
-- 2. 插入员工数据 (Users)
-- 密码默认: 123, Role: 0=员工, 1=检验员
-- =============================================
INSERT INTO Users (Name, EmployeeId, Password, Role) VALUES
-- 装配组
('敬杰', '58', '123', 0),
('汪梦萍', '33', '123', 0),
('黄神龙', '68', '123', 0),
('黄小容', '10', '123', 0),
('张弛', '27', '123', 0),
('赖丽莎', '116', '123', 0),
('唐春', '118', '123', 0),
('郭强', '32', '123', 0),
('林圣杰', '26', '123', 0),
('罗欢', '38', '123', 0),
('周贤英', '55', '123', 0),
('周娇', '69', '123', 0),
('杨雨茹', '40', '123', 0),
-- 传感器组
('张亚玲', '19', '123', 0),
('代佳灵', '29', '123', 0),
('徐浩文', '21', '123', 0),
('郭贤', '17', '123', 0),
('刘翼', '48', '123', 0),
('易坤', '49', '123', 0),
('王烨', '46', '123', 0),
('张信', '47', '123', 0),
('秦岭翔', '123', '123', 0),
-- 管理员/检验员
('管理员', 'admin', '123', 1),
('检验员', '999', '123', 1);

-- =============================================
-- 3. 插入压接工具 (CrimpingTools)
-- =============================================
INSERT INTO CrimpingTools (Model, Type) VALUES
('975236', '手动'),
('975304', '手动'),
('AFM8', '手动'),
('LX-16-A', '手动'),
('WAGO 0206-1204', '手动'),
('EM-6B2', '机器'),
('FEK-60EM', '机器');

-- =============================================
-- 4. 插入端子规格 (TerminalSpecs)
-- Method: 0=坑压, 1=模压
-- =============================================
INSERT INTO TerminalSpecs (MaterialCode, Name, Description, Method) VALUES
('2000473580', '预绝缘双线管状端头', 'H1.0/15\\WEIDMULLER魏德米勒\\红\\压接\\管', 0),
('2000448061', '针型预绝缘端头', 'HPTNYD5.5-13\\浙江华西科技有限公司\\黄色\\压接\\针', 1),
('2000032022', '管形接线端子片', 'H0.5\\12\\H0.5\\12\\WEIDMULLER（魏德米勒）\\无\\压接\\管形', 0),
('2000447702', '母预绝缘端头', 'HFDNYD5.5-250\\浙江华西科技有限公司', 1),
('2000380857', '接线端子片', '单线\\H2.5/19D\\魏德米勒\\蓝色\\压接\\管形', 0),
('2000141497', '接线端子片', 'UD.JZ2.5-4\\萧山新宇\\压接\\U形', 1),
('2010003965', '接线端子片', 'UD.JZ2.5-3\\萧山新宇\\蓝色\\U型', 1),
('2000414307', '接线端子片', 'UD.JZ 1-4\\UD.JZ 1-4\\杭州萧山新宇\\U形', 1),
('2000141597', '接线端子片', 'TNR8-8\\压接\\圆形', 1),
('2000141596', '接线端子片', 'TNR8-6\\TNR8-6\\压接\\圆形', 0),
('2000305791', '接线端子片', 'RTB1.5/10JT\\RTB1.5/10JT\\压接\\管形', 0),
('2000004691', '接线端子片', 'OD·JZ6.5-6\\杭州萧山新宇\\黄色\\压接\\圆形', 1),
('2000001872', '接线端子片', 'OD.JZ2.5-6\\OD.JZ2.5-6\\(萧山新宇)\\螺钉紧固\\无', 1),
('2000002461', '接线端子片', 'OD.JZ1-4\\(萧山新宇)\\无\\无', 1),
('2000249082', '接线端子片', 'H4.0/20D\\魏德米勒', 0),
('2000002460', '接线端子片', 'H1/14\\WEIDMULLER(魏德米勒)\\无\\无', 0),
('2000001860', '接线端子片', 'H1.5\\14\\H1.5\\14\\WEIDMULLER(魏德米勒)\\无\\压接\\管形', 0),
('2000033570', '接线端子片', 'H1.5\\12\\H1.5\\12\\魏德米勒\\无\\管形', 0),
('2000012774', '接线端子片', 'H1.5/16\\双线\\WEIDMULLER魏德米勒\\无\\压接\\管形', 0),
('2000249085', '接线端子片', 'H1.0/16', 0),
('2000249081', '接线端子片', 'H0.75/16\\魏德米勒', 0),
('2000001863', '接线端子片', 'H0.5\\14\\H0.5\\14\\WEIDMULLER魏德米勒\\无\\压接\\管形', 0),
('2000249087', '接线端子片', 'H0.5/16', 0),
('2000184705', '接线端子片', 'AI1-10RD\\PHOENIX菲尼克斯\\压接\\管形', 0),
('2000271971', '接线端子片', '6.5-4\\OD.JZ6.5-4\\压接\\圆形', 1),
('2000456329', '接线端子片', 'UD.JZ 1-3\\杭州萧山新宇\\压接\\U形', 1),
('2000011036', '圆形接线端子片', 'OD·JZ2.5-4\\OD·JZ2.5-4\\杭州萧山新宇\\压接\\圆形', 1),
('2000406187', '双线管状端头', 'H2.5/21D\\WEIDMULLER魏德米勒\\蓝\\压接\\管形', 0),
('2000406188', '双线管状端头', 'H1.5/20\\WEIDMULLER魏德米勒\\红\\压接\\管形', 0),
('2000406186', '双线管状端头', 'H1.0/19\\WEIDMULLER魏德米勒\\黄\\压接\\管形', 0),
('2000477309', '双线管状端头', 'H0.5/14\\WEIDMULLER魏德米勒\\桔红\\压接\\管形', 0),
('2000366583', '压接端子', 'OD.JZ6.5-8\\压接\\圆形', 1),
('2000366606', '压接端子', 'OD.JZ6.5-5\\压接\\圆形', 1),
('2000184851', '接线端子片', 'RV3.5-4\\雷欣特\\压接\\圆形', 1);

-- =============================================
-- 5. 插入导线规格 (WireSpecs)
-- 根据文档表格整理，保留了特殊描述的线材
-- =============================================
INSERT INTO WireSpecs (Id, DisplayName, SectionArea) VALUES
('W-0.1', '0.1 mm²', 0.1),
('W-0.12', '0.12 mm²', 0.12),
('W-0.2', '12*0.15 (0.2 mm²)', 0.2),
('W-0.3', '16*0.15 (0.3 mm²)', 0.3),
('W-0.35', '19*0.15 (0.35 mm²)', 0.35),
('W-0.4', '23*0.15 (0.4 mm²)', 0.4),
('W-0.5', '16*0.2 (0.5 mm²)', 0.5),
('W-0.6', '19*0.2 (0.6 mm²)', 0.6),
('W-0.75', '42*0.15 / 24*0.2 (0.75 mm²)', 0.75),
('W-0.8', '42*0.15 (0.8 mm²)', 0.8),
('W-1.0', '32*0.2 (1.0 mm²)', 1.0),
('W-1.2', '40*0.2 (1.2 mm²)', 1.2),
('W-1.5', '19*0.32 / 30*0.25 (1.5 mm²)', 1.5),
('W-2.0', '40*0.25 (2.0 mm²)', 2.0),
('W-2.5', '19*0.41 / 49*0.25 (2.5 mm²)', 2.5),
('W-3.0', '3.0 mm²', 3.0),
('W-4.0', '56*0.3 / 19*0.52 (4.0 mm²)', 4.0),
('W-5.0', '5.0 mm²', 5.0),
('W-6.0', '84*0.3 (6.0 mm²)', 6.0),
('W-8.0', '8.0 mm²', 8.0),
('W-10.0', '10.0 mm²', 10.0),
('W-16.0', '16.0 mm²', 16.0),
('W-20.0', '20.0 mm²', 20.0),
('W-25.0', '25.0 mm²', 25.0),
('W-30.0', '30.0 mm²', 30.0),
('W-35.0', '35.0 mm²', 35.0),
('W-40.0', '40.0 mm²', 40.0),
('W-45.0', '45.0 mm²', 45.0),
('W-50.0', '50.0 mm²', 50.0),
('W-55.0', '55.0 mm²', 55.0),
('W-60.0', '60.0 mm²', 60.0),
('W-65.0', '65.0 mm²', 65.0),
('W-70.0', '70.0 mm²', 70.0),
('W-75.0', '75.0 mm²', 75.0),
('W-80.0', '80.0 mm²', 80.0),
('W-85.0', '85.0 mm²', 85.0),
('W-90.0', '90.0 mm²', 90.0),
('W-100.0', '100.0 mm²', 100.0),
('W-110.0', '110.0 mm²', 110.0),
('W-120.0', '120.0 mm²', 120.0);

-- =============================================
-- 6. 插入拉力标准规则 (PullForceStandards)
-- Method: 0=坑压, 1=模压, 2=B型
-- =============================================
INSERT INTO PullForceStandards (Method, SectionArea, StandardValue) VALUES
-- 0.1
(0, 0.1, 16), (1, 0.1, 25), (2, 0.1, 30),
-- 0.12
(0, 0.12, 20), (1, 0.12, 30), (2, 0.12, 17),
-- 0.2
(0, 0.2, 34), (1, 0.2, 44), (2, 0.2, 28),
-- 0.3
(0, 0.3, 51), (1, 0.3, 60), (2, 0.3, 40),
-- 0.35
(0, 0.35, 60), (1, 0.35, 70), (2, 0.35, 46),
-- 0.4
(0, 0.4, 68), (1, 0.4, 75), (2, 0.4, 54),
-- 0.5
(0, 0.5, 85), (1, 0.5, 85), (2, 0.5, 65),
-- 0.6
(0, 0.6, 100), (1, 0.6, 120), (2, 0.6, 75),
-- 0.75
(0, 0.75, 129), (1, 0.75, 150), (2, 0.75, 90),
-- 0.8
(0, 0.8, 138), (1, 0.8, 160), (2, 0.8, 98),
-- 1.0
(0, 1.0, 172), (1, 1.0, 190), (2, 1.0, 120),
-- 1.2
(0, 1.2, 206), (1, 1.2, 210), (2, 1.2, 140),
-- 1.5
(0, 1.5, 248), (1, 1.5, 240), (2, 1.5, 160),
-- 2.0
(0, 2.0, 300), (1, 2.0, 300), (2, 2.0, 210),
-- 2.5
(0, 2.5, 375), (1, 2.5, 370), (2, 2.5, 250),
-- 3.0
(0, 3.0, 450), (1, 3.0, 450), (2, 3.0, 280),
-- 4.0
(0, 4.0, 600), (1, 4.0, 560), (2, 4.0, 340),
-- 5.0 (坑压无)
(1, 5.0, 650), (2, 5.0, 400),
-- 6.0
(1, 6.0, 750), (2, 6.0, 460),
-- 8.0 (仅模压)
(1, 8.0, 950),
-- 10.0
(1, 10.0, 1120),
-- 16.0
(1, 16.0, 1500),
-- 20.0
(1, 20.0, 1700),
-- 25.0
(1, 25.0, 1980),
-- 30.0
(1, 30.0, 2250),
-- 35.0
(1, 35.0, 2500),
-- 40.0
(1, 40.0, 2800),
-- 45.0
(1, 45.0, 2900),
-- 50.0
(1, 50.0, 3000),
-- 55.0
(1, 55.0, 3100),
-- 60.0
(1, 60.0, 3200),
-- 65.0
(1, 65.0, 3300),
-- 70.0
(1, 70.0, 3350),
-- 75.0
(1, 75.0, 3400),
-- 80.0
(1, 80.0, 3500),
-- 85.0
(1, 85.0, 3650),
-- 90.0
(1, 90.0, 3700),
-- 100.0
(1, 100.0, 3800),
-- 110.0
(1, 110.0, 3850),
-- 120.0
(1, 120.0, 3900);


select * from PullForceStandards;

select *
from terminalsamples;

select *
from inspectionrecords;

DELETE FROM ProductionOrders;

ALTER TABLE ProductionOrders
ADD COLUMN IsClosed TINYINT NOT NULL DEFAULT 0 AFTER CreatorName;
