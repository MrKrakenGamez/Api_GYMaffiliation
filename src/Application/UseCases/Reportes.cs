using AutoMapper;
using GymAffiliate.Application.DTOs.Requests;
using GymAffiliate.Application.DTOs.Responses;
using GymAffiliate.Domain.Interfaces.Repositories;
using GymAffiliate.Shared.Result;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GymAffiliate.Application.UseCases.Reportes;

// ─────────────────────────────────────────────────────────────────────────────
// ReporteIngresosHandler
// ─────────────────────────────────────────────────────────────────────────────
public sealed class ReporteIngresosHandler(IReporteRepository repo, IMapper mapper)
{
    public async Task<Result<IEnumerable<IngresoMensualResponse>>> HandleAsync(
        ReporteIngresosRequest request, int? userId, CancellationToken ct = default)
    {
        var result = await repo.IngresosAsync(request.Year, request.Month, request.BranchId, userId, ct);
        if (result.IsFailure) return result.Map(_ => Enumerable.Empty<IngresoMensualResponse>());
        return Result<IEnumerable<IngresoMensualResponse>>.Success(
            mapper.Map<IEnumerable<IngresoMensualResponse>>(result.Value));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// AfiliadosActivosHandler
// ─────────────────────────────────────────────────────────────────────────────
public sealed class AfiliadosActivosHandler(IReporteRepository repo, IMapper mapper)
{
    public async Task<Result<IEnumerable<AfiliadoEstadoResponse>>> HandleAsync(
        int? branchId, int? userId, CancellationToken ct = default)
    {
        var result = await repo.AfiliadosActivosAsync(branchId, userId, ct);
        if (result.IsFailure) return result.Map(_ => Enumerable.Empty<AfiliadoEstadoResponse>());
        return Result<IEnumerable<AfiliadoEstadoResponse>>.Success(
            mapper.Map<IEnumerable<AfiliadoEstadoResponse>>(result.Value));
    }
}