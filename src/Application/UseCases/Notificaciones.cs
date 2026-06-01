using AutoMapper;
using FluentValidation;
using GymAffiliate.Application.DTOs.Requests;
using GymAffiliate.Application.DTOs.Responses;
using GymAffiliate.Domain.Interfaces.Repositories;
using GymAffiliate.Shared.Errors;
using GymAffiliate.Shared.Result;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GymAffiliate.Application.UseCases.Notificaciones;

// ─────────────────────────────────────────────────────────────────────────────
// VencimientosHandler
// ─────────────────────────────────────────────────────────────────────────────
public sealed class VencimientosHandler(INotificacionRepository repo, IMapper mapper)
{
    public async Task<Result<IEnumerable<VencimientoResponse>>> HandleAsync(
        int daysAhead, int? userId, CancellationToken ct = default)
    {
        var result = await repo.ObtenerPorVencerAsync(daysAhead, userId, ct);
        if (result.IsFailure) return result.Map(_ => Enumerable.Empty<VencimientoResponse>());
        return Result<IEnumerable<VencimientoResponse>>.Success(
            mapper.Map<IEnumerable<VencimientoResponse>>(result.Value));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// EnviarAlertaHandler
// ─────────────────────────────────────────────────────────────────────────────
public sealed class EnviarAlertaHandler(
    INotificacionRepository repo,
    IValidator<EnviarAlertaRequest> validator)
{
    public async Task<Result<AlertaEnviadaResponse>> HandleAsync(
        EnviarAlertaRequest request, int? userId, string? session, CancellationToken ct = default)
    {
        var val = await validator.ValidateAsync(request, ct);
        if (!val.IsValid)
        {
            var errors = val.Errors.GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            return Result<AlertaEnviadaResponse>.Failure(new ResultError(ErrorCodes.ErrorValidacion, "Errores de validación.", 422, errors));
        }

        var result = await repo.GenerarAlertasAsync(request.DaysAhead, request.Channel, userId, session, ct);
        if (result.IsFailure) return result.Map(_ => (AlertaEnviadaResponse)null!);
        return Result<AlertaEnviadaResponse>.Success(
            //new AlertaEnviadaResponse(result.Value, $"{result.Value} notificaciones generadas.")
                new AlertaEnviadaResponse
                {
                    NotificationsGenerated = result.Value,
                    Message = $"{result.Value} notificaciones generadas."
                }
            );
    }
}