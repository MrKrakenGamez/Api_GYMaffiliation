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

namespace GymAffiliate.Application.UseCases.Pagos;

// ─────────────────────────────────────────────────────────────────────────────
// RegistrarPagoHandler
// ─────────────────────────────────────────────────────────────────────────────
public sealed class RegistrarPagoHandler(IPagoRepository repo, IValidator<RegistrarPagoRequest> validator)
{
    public async Task<Result<RegistrarPagoResponse>> HandleAsync(
        RegistrarPagoRequest request,
        int? userId, string? ip, string? session,
        CancellationToken ct = default)
    {
        var val = await validator.ValidateAsync(request, ct);
        if (!val.IsValid)
        {
            var errors = val.Errors.GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            return Result<RegistrarPagoResponse>.Failure(new ResultError(ErrorCodes.ErrorValidacion, "Errores de validación.", 422, errors));
        }

        var result = await repo.RegistrarAsync(new RegistrarPagoParams(
            request.AffiliateId, request.MembershipId, request.PaymentMethodId,
            request.Amount, request.ReferenceNumber, request.Notes,
            userId, ip, session), ct);

        if (result.IsFailure) return result.Map(_ => (RegistrarPagoResponse)null!);
        var (paymentId, receipt) = result.Value;
        return Result<RegistrarPagoResponse>.Success(
        new RegistrarPagoResponse
        {
            PaymentId = paymentId,
            ReceiptNumber = receipt,
            Message = "Pago registrado exitosamente."
        });
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// HistorialPagosHandler
// ─────────────────────────────────────────────────────────────────────────────
public sealed class HistorialPagosHandler(IPagoRepository repo, IMapper mapper)
{
    public async Task<Result<IEnumerable<PagoResponse>>> HandleAsync(
        ListarPagosRequest request, int? userId, CancellationToken ct = default)
    {
        var result = await repo.ListarAsync(
            request.AffiliateId, request.From, request.To, request.BranchId, userId, ct);
        if (result.IsFailure) return result.Map(_ => Enumerable.Empty<PagoResponse>());
        return Result<IEnumerable<PagoResponse>>.Success(mapper.Map<IEnumerable<PagoResponse>>(result.Value));
    }
}