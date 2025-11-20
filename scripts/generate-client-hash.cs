using System;
using System.Security.Cryptography;
using System.Text;

// Script temporário para gerar hash PBKDF2 do client secret
// Execute: dotnet script scripts/generate-client-hash.cs

const string secret = "$2a$12$vzIWMYbiLEI4RvZ/J1dfpOay6FTs4dqfe8RxGsQBmJ9ihI9Ct9nju";
const int SaltSize = 32;
const int KeySize = 32;
const int Iterations = 100_000;

// Gerar salt fixo para reproduzibilidade (ou use RandomNumberGenerator para produção)
var salt = new byte[SaltSize];
Array.Fill<byte>(salt, 0x00); // Para teste, use um salt fixo ou gere aleatoriamente

// Derive key usando PBKDF2
byte[] hash;
using (var pbkdf2 = new Rfc2898DeriveBytes(secret, salt, Iterations, HashAlgorithmName.SHA256))
{
    hash = pbkdf2.GetBytes(KeySize);
}

// Converter para hex para SQL
var hashHex = Convert.ToHexString(hash);
var saltHex = Convert.ToHexString(salt);

Console.WriteLine("-- Hash e Salt para inserção SQL:");
Console.WriteLine($"SecretHash (hex): 0x{hashHex}");
Console.WriteLine($"SecretSalt (hex): 0x{saltHex}");
Console.WriteLine();
Console.WriteLine("-- Script SQL completo:");
Console.WriteLine($@"
DECLARE @ClientId UNIQUEIDENTIFIER = NEWID();
DECLARE @ClientIdentifier NVARCHAR(64) = 'client-omni-flow';
DECLARE @Hash VARBINARY(MAX) = 0x{hashHex};
DECLARE @Salt VARBINARY(MAX) = 0x{saltHex};

INSERT INTO Clients (Id, Name, ClientIdentifier, SecretHash, SecretSalt, Status, CreatedAt)
VALUES (@ClientId, 'OmniFlow', @ClientIdentifier, @Hash, @Salt, 'Active', GETUTCDATE());

SELECT Id, Name, ClientIdentifier, Status, CreatedAt FROM Clients WHERE ClientIdentifier = @ClientIdentifier;
");

