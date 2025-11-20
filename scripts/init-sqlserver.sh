#!/bin/bash
# Script para inicializar SQL Server e criar banco de dados DataFlowDev

echo "Waiting for SQL Server to be ready..."
sleep 10

/opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "DataFlow@Dolar$" -Q "
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'DataFlowDev')
BEGIN
    CREATE DATABASE DataFlowDev;
    PRINT 'Database DataFlowDev created successfully.';
END
ELSE
BEGIN
    PRINT 'Database DataFlowDev already exists.';
END
GO

-- Criar login e usuário
IF NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = 'user_data_flow_db')
BEGIN
    CREATE LOGIN user_data_flow_db WITH PASSWORD = 'DataFlow@Dolar$';
    PRINT 'Login user_data_flow_db created successfully.';
END
ELSE
BEGIN
    PRINT 'Login user_data_flow_db already exists.';
END
GO

USE DataFlowDev;
GO

IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = 'user_data_flow_db')
BEGIN
    CREATE USER user_data_flow_db FOR LOGIN user_data_flow_db;
    ALTER ROLE db_owner ADD MEMBER user_data_flow_db;
    PRINT 'User user_data_flow_db created and granted db_owner role.';
END
ELSE
BEGIN
    PRINT 'User user_data_flow_db already exists.';
END
GO
" -C

echo "SQL Server initialization completed."

