using System.Net;
using DataFlow.Api.Services.Interfaces;
using DataFlow.Core.Domain.Entities;
using DataFlow.Observability;
using Microsoft.AspNetCore.Http;

namespace DataFlow.Api.Middleware;

public class ClientRateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ClientRateLimitMiddleware> _logger;
    private readonly IRedisRateLimiter _rateLimiter;
    private const int DefaultRateLimitPerMinute = 30;

    public ClientRateLimitMiddleware(
        RequestDelegate next,
        ILogger<ClientRateLimitMiddleware> logger,
        IRedisRateLimiter rateLimiter)
    {
        _next = next;
        _logger = logger;
        _rateLimiter = rateLimiter;
    }

    public async Task InvokeAsync(HttpContext context, IClientCredentialValidator credentialValidator)
    {
        // Aplicar rate limiting apenas em POST /imports
        if (!context.Request.Path.StartsWithSegments("/imports") || 
            context.Request.Method != "POST")
        {
            await _next(context);
            return;
        }

        var client = await credentialValidator.ValidateAsync(context.Request);
        if (client == null)
        {
            // Deixar o middleware de autenticação tratar
            await _next(context);
            return;
        }

        // Buscar limite da ClientPolicy, com fallback para padrão
        var limit = GetRateLimitFromPolicy(client);
        var period = TimeSpan.FromMinutes(1);
        var key = $"client:{client.ClientIdentifier}";
        
        var decision = await _rateLimiter.AllowAsync(key, limit, period);

        if (!decision.IsAllowed)
        {
            Metrics.RateLimit429Counter.Add(1, new KeyValuePair<string, object?>("client.id", client.ClientIdentifier));
            
            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            context.Response.Headers["Retry-After"] = (decision.RetryAfterSeconds ?? 60).ToString();
            context.Response.Headers["X-RateLimit-Limit"] = limit.ToString();
            context.Response.Headers["X-RateLimit-Remaining"] = "0";
            
            _logger.LogWarning(
                "Rate limit exceeded for client {ClientId} ({ClientIdentifier}), limit: {Limit}/min, retry after {RetryAfter}s",
                client.Id, client.ClientIdentifier, limit, decision.RetryAfterSeconds ?? 60);
            
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Rate limit exceeded",
                retryAfter = decision.RetryAfterSeconds ?? 60
            });
            
            return;
        }

        context.Response.Headers["X-RateLimit-Limit"] = limit.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = decision.Remaining.ToString();
        await _next(context);
    }

    private static int GetRateLimitFromPolicy(Client client)
    {
        // Buscar a primeira policy ativa do cliente
        var policy = client.Policies?.FirstOrDefault();
        if (policy?.RateLimitPerMinute.HasValue == true)
        {
            return policy.RateLimitPerMinute.Value;
        }

        // Fallback para padrão
        return DefaultRateLimitPerMinute;
    }
}

