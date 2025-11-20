// ============================================
// EXEMPLO COMPLETO DE ENDPOINTS PARA OMNIFLOW
// ============================================
// Este arquivo contém exemplos completos de implementação
// dos endpoints que o OmniFlow precisa expor para receber
// lotes de importação do DataFlow
// ============================================

using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace OmniFlow.Api.Controllers;

// ============================================
// 1. DTOs de Request/Response
// ============================================

public class ImportLeadsRequest
{
    public Guid BatchId { get; set; }
    public int ChunkId { get; set; }
    public int Offset { get; set; }
    public List<LeadImportItem> Items { get; set; } = new();
}

public class LeadImportItem
{
    public string? PhoneNumber { get; set; }
    public string? Origin { get; set; }
    public Dictionary<string, object>? PropertyContextJson { get; set; }
    public Dictionary<string, object>? LeadContextJson { get; set; }
    // Adicione outros campos conforme necessário
}

public class ImportItemResult
{
    public bool Success { get; set; }
    public Guid? LeadId { get; set; }
    public string? Error { get; set; }
    public int? Sequence { get; set; }
}

public class ImportLeadsResponse
{
    public Guid BatchId { get; set; }
    public int ChunkId { get; set; }
    public int Processed { get; set; }
    public int Errors { get; set; }
    public List<ImportItemResult> Items { get; set; } = new();
    public string? Message { get; set; }
}

public class ChunkAckRequest
{
    public Guid BatchId { get; set; }
    public int ChunkId { get; set; }
    public Guid? AckId { get; set; }
    public string Status { get; set; } = "Processed";
    public int ProcessedCount { get; set; }
    public int ErrorCount { get; set; }
    public List<string>? Errors { get; set; }
}

// ============================================
// 2. Controller Principal - POST /api/leads/import
// ============================================

[ApiController]
[Route("api/[controller]")]
public class LeadsController : ControllerBase
{
    private readonly ILeadService _leadService;
    private readonly ILogger<LeadsController> _logger;
    private readonly IConfiguration _configuration;

    public LeadsController(
        ILeadService leadService,
        ILogger<LeadsController> logger,
        IConfiguration configuration)
    {
        _leadService = leadService;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Recebe lote de leads importados do DataFlow
    /// </summary>
    /// <param name="request">Lote de leads para importar</param>
    /// <returns>Resultado do processamento do lote</returns>
    [HttpPost("import")]
    public async Task<ActionResult<ImportLeadsResponse>> ImportLeads(
        [FromBody] ImportLeadsRequest request)
    {
        // Validação básica
        if (request == null)
        {
            return BadRequest(new { message = "Request body is required" });
        }

        if (request.Items == null || !request.Items.Any())
        {
            return BadRequest(new { message = "Items are required and cannot be empty" });
        }

        if (request.BatchId == Guid.Empty)
        {
            return BadRequest(new { message = "BatchId is required" });
        }

        _logger.LogInformation(
            "Received import batch {BatchId}, chunk {ChunkId}, offset {Offset}, items {Count}",
            request.BatchId, request.ChunkId, request.Offset, request.Items.Count);

        var results = new List<ImportItemResult>();
        var processedCount = 0;
        var errorCount = 0;

        // Processar cada item do lote
        for (int i = 0; i < request.Items.Count; i++)
        {
            var item = request.Items[i];
            var sequence = request.Offset + i;

            try
            {
                // Validar item
                if (string.IsNullOrWhiteSpace(item.PhoneNumber))
                {
                    results.Add(new ImportItemResult
                    {
                        Success = false,
                        Error = "PhoneNumber is required",
                        Sequence = sequence
                    });
                    errorCount++;
                    continue;
                }

                // Aplicar regras de negócio do OmniFlow
                // Exemplo: buscar contexto do usuário, validar permissões, etc.
                var lead = await _leadService.CreateOrUpdateLeadAsync(
                    phoneNumber: item.PhoneNumber,
                    origin: item.Origin ?? request.BatchId.ToString(), // fallback
                    propertyContext: item.PropertyContextJson,
                    leadContext: item.LeadContextJson,
                    batchId: request.BatchId,
                    requestedBy: "DataFlow" // ou extrair de metadata
                );

                results.Add(new ImportItemResult
                {
                    Success = true,
                    LeadId = lead.Id,
                    Sequence = sequence
                });
                processedCount++;

                _logger.LogDebug(
                    "Processed lead {LeadId} from batch {BatchId}, chunk {ChunkId}, sequence {Sequence}",
                    lead.Id, request.BatchId, request.ChunkId, sequence);
            }
            catch (ValidationException ex)
            {
                _logger.LogWarning(ex,
                    "Validation error processing item {Sequence} from batch {BatchId}, chunk {ChunkId}",
                    sequence, request.BatchId, request.ChunkId);

                results.Add(new ImportItemResult
                {
                    Success = false,
                    Error = ex.Message,
                    Sequence = sequence
                });
                errorCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error processing item {Sequence} from batch {BatchId}, chunk {ChunkId}",
                    sequence, request.BatchId, request.ChunkId);

                results.Add(new ImportItemResult
                {
                    Success = false,
                    Error = $"Internal error: {ex.Message}",
                    Sequence = sequence
                });
                errorCount++;
            }
        }

        var response = new ImportLeadsResponse
        {
            BatchId = request.BatchId,
            ChunkId = request.ChunkId,
            Processed = processedCount,
            Errors = errorCount,
            Items = results,
            Message = errorCount > 0
                ? $"Processed {processedCount} leads with {errorCount} errors"
                : $"Successfully processed {processedCount} leads"
        };

        // Retornar 200 OK mesmo com erros parciais
        // O DataFlow vai registrar os erros em ImportItem
        return Ok(response);
    }
}

// ============================================
// 3. Controller de ACK (Opcional)
// ============================================

[ApiController]
[Route("imports")]
public class ImportsController : ControllerBase
{
    private readonly ILogger<ImportsController> _logger;
    private readonly IImportAckService _ackService;

    public ImportsController(
        ILogger<ImportsController> logger,
        IImportAckService ackService)
    {
        _logger = logger;
        _ackService = ackService;
    }

    /// <summary>
    /// Endpoint opcional para confirmar processamento de um chunk
    /// Útil se o OmniFlow processar assincronamente e quiser notificar depois
    /// </summary>
    [HttpPost("{batchId}/acks")]
    public async Task<IActionResult> AcknowledgeChunk(
        Guid batchId,
        [FromBody] ChunkAckRequest request)
    {
        if (request == null)
        {
            return BadRequest(new { message = "Request body is required" });
        }

        if (request.BatchId != batchId)
        {
            return BadRequest(new { message = "BatchId mismatch" });
        }

        _logger.LogInformation(
            "Received ACK for batch {BatchId}, chunk {ChunkId}, status {Status}, processed {Processed}, errors {Errors}",
            request.BatchId, request.ChunkId, request.Status, request.ProcessedCount, request.ErrorCount);

        try
        {
            // Registrar o ACK (opcional: persistir no banco para auditoria)
            await _ackService.RegisterAckAsync(request);

            return Ok(new
            {
                message = "ACK received",
                batchId = request.BatchId,
                chunkId = request.ChunkId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing ACK for batch {BatchId}, chunk {ChunkId}",
                request.BatchId, request.ChunkId);
            return StatusCode(500, new { message = "Error processing ACK" });
        }
    }
}

// ============================================
// 4. Interface do Serviço de Leads (exemplo)
// ============================================

public interface ILeadService
{
    Task<Lead> CreateOrUpdateLeadAsync(
        string phoneNumber,
        string? origin,
        Dictionary<string, object>? propertyContext,
        Dictionary<string, object>? leadContext,
        Guid batchId,
        string? requestedBy);
}

public class Lead
{
    public Guid Id { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Origin { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ============================================
// 5. Interface do Serviço de ACK (opcional)
// ============================================

public interface IImportAckService
{
    Task RegisterAckAsync(ChunkAckRequest request);
}

// ============================================
// 6. Exceção customizada para validação
// ============================================

public class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }
    public ValidationException(string message, Exception innerException) 
        : base(message, innerException) { }
}

// ============================================
// NOTAS DE IMPLEMENTAÇÃO:
// ============================================
// 1. O endpoint POST /api/leads/import é OBRIGATÓRIO
//    - Recebe lotes do DataFlow
//    - Processa cada item aplicando regras de negócio
//    - Retorna resultado detalhado por item
//
// 2. O endpoint POST /imports/{batchId}/acks é OPCIONAL
//    - Útil se o OmniFlow processar assincronamente
//    - Permite notificar o DataFlow depois do processamento
//    - Se não implementar, o DataFlow considera sucesso com 200 OK
//
// 3. Headers de autenticação:
//    - X-Client-Id e X-Client-Secret já estão implementados
//    - Adicione middleware de autenticação se necessário
//
// 4. Rate limiting:
//    - Considere implementar rate limiting por batchId
//    - Evite sobrecarregar o banco com muitos inserts simultâneos
//
// 5. Transações:
//    - Processe items em transação se necessário
//    - Rollback em caso de erro crítico
//
// 6. Logging:
//    - Registre batchId, chunkId, sequence para rastreabilidade
//    - Use correlation IDs para facilitar debugging
// ============================================

