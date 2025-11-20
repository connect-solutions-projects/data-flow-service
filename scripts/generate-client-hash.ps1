# Script PowerShell para gerar hash PBKDF2 do client secret
# Execute: pwsh scripts/generate-client-hash.ps1

$secret = "$2a$12$vzIWMYbiLEI4RvZ/J1dfpOay6FTs4dqfe8RxGsQBmJ9ihI9Ct9nju"
$saltSize = 32
$keySize = 32
$iterations = 100000

# Gerar salt aleatório
$salt = New-Object byte[] $saltSize
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($salt)
$rng.Dispose()

# Derive key usando PBKDF2
$pbkdf2 = New-Object System.Security.Cryptography.Rfc2898DeriveBytes(
    $secret,
    $salt,
    $iterations,
    [System.Security.Cryptography.HashAlgorithmName]::SHA256
)
$hash = $pbkdf2.GetBytes($keySize)
$pbkdf2.Dispose()

# Converter para hex
$hashHex = [System.BitConverter]::ToString($hash).Replace("-", "")
$saltHex = [System.BitConverter]::ToString($salt).Replace("-", "")

Write-Host "`n-- Hash e Salt para inserção SQL:" -ForegroundColor Green
Write-Host "SecretHash (hex): 0x$hashHex"
Write-Host "SecretSalt (hex): 0x$saltHex"
Write-Host "`n-- Script SQL completo:" -ForegroundColor Green
Write-Host @"
DECLARE @ClientId UNIQUEIDENTIFIER = NEWID();
DECLARE @ClientIdentifier NVARCHAR(64) = 'client-omni-flow';
DECLARE @Hash VARBINARY(MAX) = 0x$hashHex;
DECLARE @Salt VARBINARY(MAX) = 0x$saltHex;

INSERT INTO Clients (Id, Name, ClientIdentifier, SecretHash, SecretSalt, Status, CreatedAt)
VALUES (@ClientId, 'OmniFlow', @ClientIdentifier, @Hash, @Salt, 'Active', GETUTCDATE());

SELECT Id, Name, ClientIdentifier, Status, CreatedAt FROM Clients WHERE ClientIdentifier = @ClientIdentifier;
"@

