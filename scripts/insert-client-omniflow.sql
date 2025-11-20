-- Script SQL para inserir cliente OmniFlow no DataFlow
-- ClientId: client-omni-flow
-- Secret: $2a$12$vzIWMYbiLEI4RvZ/J1dfpOay6FTs4dqfe8RxGsQBmJ9ihI9Ct9nju
-- Hash gerado via PBKDF2 (SHA256, 100k iterações, 32 bytes)

USE [DataFlowDev];
GO

-- Verificar se já existe
IF EXISTS (SELECT 1 FROM Clients WHERE ClientIdentifier = 'client-omni-flow')
BEGIN
    PRINT 'Cliente client-omni-flow já existe. Removendo...';
    DELETE FROM Clients WHERE ClientIdentifier = 'client-omni-flow';
END
GO

-- Inserir novo cliente
DECLARE @ClientId UNIQUEIDENTIFIER = NEWID();
DECLARE @ClientIdentifier NVARCHAR(64) = 'client-omni-flow';
DECLARE @Hash VARBINARY(MAX) = 0xEBC33FC2D23F5150DA4203CCC804FD3105B3BE717808C6C8B30E2DEB0BF27135;
DECLARE @Salt VARBINARY(MAX) = 0x66DE0640A2C8EDC0FB2EDEF72399772ED9992FF4DAE6AE77DCFABE7A230CBB4B;

INSERT INTO Clients (Id, Name, ClientIdentifier, SecretHash, SecretSalt, Status, CreatedAt)
VALUES (@ClientId, 'OmniFlow', @ClientIdentifier, @Hash, @Salt, 'Active', GETUTCDATE());

-- Verificar inserção
SELECT 
    Id, 
    Name, 
    ClientIdentifier, 
    Status, 
    CreatedAt,
    LEN(SecretHash) AS SecretHashLength,
    LEN(SecretSalt) AS SecretSaltLength
FROM Clients 
WHERE ClientIdentifier = @ClientIdentifier;

PRINT 'Cliente OmniFlow inserido com sucesso!';
GO

