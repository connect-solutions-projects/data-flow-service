namespace DataFlow.Core.Application.Options;

public class SensitiveDataOptions
{
    public const string SectionName = "SensitiveData";

    /// <summary>
    /// Redact payloads stored em ImportItems quando o chunk for enviado com sucesso.
    /// </summary>
    public bool RedactPayloadOnSuccess { get; set; } = true;

    /// <summary>
    /// Também redigir payloads quando o chunk falhar (útil para ambientes altamente restritivos).
    /// </summary>
    public bool RedactPayloadOnFailure { get; set; } = false;

    /// <summary>
    /// Inclui hash SHA-256 no payload mascarado para permitir auditoria/deduplicação.
    /// </summary>
    public bool IncludePayloadHash { get; set; } = true;
}

