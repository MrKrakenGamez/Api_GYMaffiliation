using AutoMapper;
using FluentValidation;
using GymAffiliate.Application.DTOs.Requests;
using GymAffiliate.Application.DTOs.Responses;
using GymAffiliate.Domain.Interfaces.Repositories;
using GymAffiliate.Shared.Errors;
using GymAffiliate.Shared.Result;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GymAffiliate.Application.UseCases.Acceso;

// ─────────────────────────────────────────────────────────────────────────────
// RegistrarIngresoHandler
// ─────────────────────────────────────────────────────────────────────────────
public sealed class RegistrarIngresoHandler(
    IAccesoRepository repo,
    IMapper mapper,
    IValidator<RegistrarIngresoRequest> validator,
    ILogger<RegistrarIngresoHandler> log)
{
    public async Task<Result<CheckInResponse>> HandleAsync(
        RegistrarIngresoRequest request,
        int? userId, string? ip, string? session,
        CancellationToken ct = default)
    {

        var val = await validator.ValidateAsync(request, ct);
        if (!val.IsValid)
        {
            var errors = val.Errors.GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            return Result<CheckInResponse>.Failure(new ResultError(ErrorCodes.ErrorValidacion, "Errores de validación.", 422, errors));
        }

        var result = await repo.RegistrarIngresoAsync(
            request.AffiliateId, request.BranchId, userId, ip, session, ct);

        if (result.IsFailure) return result.Map(_ => (CheckInResponse)null!);

        log.LogInformation("CheckIn: Afiliado={A} Sucursal={B} Acceso={G}",
            request.AffiliateId, request.BranchId, result.Value.AccessGranted);

        return Result<CheckInResponse>.Success(mapper.Map<CheckInResponse>(result.Value));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// ValidarAccesoHandler
// ─────────────────────────────────────────────────────────────────────────────
public sealed class ValidarAccesoHandler(IAccesoRepository repo, IMapper mapper)
{
    public async Task<Result<ValidacionAccesoResponse>> HandleAsync(
        int affiliateId, CancellationToken ct = default)
    {
        var result = await repo.ValidarAccesoAsync(affiliateId, ct);
        if (result.IsFailure) return result.Map(_ => (ValidacionAccesoResponse)null!);
        if (result.Value is null)
            return Result<ValidacionAccesoResponse>.Failure(ErrorCodes.AfiliadoNoEncontrado, "Afiliado no encontrado.", 404);
        return Result<ValidacionAccesoResponse>.Success(mapper.Map<ValidacionAccesoResponse>(result.Value));
    }
}
