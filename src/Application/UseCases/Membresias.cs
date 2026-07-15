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
using AutoMapper;
using Microsoft.Extensions.Logging;

namespace GymAffiliate.Application.UseCases.Membresias;

// ─────────────────────────────────────────────────────────────────────────────
// AsignarMembresiaHandler
// ─────────────────────────────────────────────────────────────────────────────
public sealed class AsignarMembresiaHandler(
    IMembresiaRepository repo,
    IValidator<AsignarMembresiaRequest> validator)
{
    public async Task<Result<MembresiaResponse>> HandleAsync(
        AsignarMembresiaRequest request,
        int? userId, string? ip, string? session,
        CancellationToken ct = default)
    {
        var val = await validator.ValidateAsync(request, ct);
        if (!val.IsValid)
        {
            var errors = val.Errors.GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            return Result<MembresiaResponse>.Failure(new ResultError(ErrorCodes.ErrorValidacion, "Errores de validación.", 422, errors));
        }

        var result = await repo.AsignarAsync(new AsignarMembresiaParams(
            request.AffiliateId, request.MembershipTypeId, request.BranchId,
            request.StartDate, request.Notes, userId, ip, session), ct);

        if (result.IsFailure) return result.Map(_ => (MembresiaResponse)null!);
        //return Result<MembresiaResponse>.Success(
        //new MembresiaResponse(result.Value, "Membresía asignada exitosamente.", null, null, null, null)

        //);
        return Result<MembresiaResponse>.Success(
        new MembresiaResponse
        {
            MembershipId = result.Value,
            Message = "Membresía asignada exitosamente.",
            TypeName = null,
            StartDate = null,
            EndDate = null,
            DaysUntilExpiry = null
        });
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// RenovarMembresiaHandler
// ─────────────────────────────────────────────────────────────────────────────
public sealed class RenovarMembresiaHandler(
    IMembresiaRepository repo,
    IValidator<RenovarMembresiaRequest> validator)
{
    public async Task<Result<MembresiaResponse>> HandleAsync(
        RenovarMembresiaRequest request,
        int? userId, string? ip, string? session,
        CancellationToken ct = default)
    {
        var val = await validator.ValidateAsync(request, ct);
        if (!val.IsValid)
        {
            var errors = val.Errors.GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            return Result<MembresiaResponse>.Failure(new ResultError(ErrorCodes.ErrorValidacion, "Errores de validación.", 422, errors));
        }

        var result = await repo.RenovarAsync(new RenovarMembresiaParams(
            request.AffiliateId, request.MembershipTypeId, request.BranchId,
            request.Notes, userId, ip, session), ct);

        if (result.IsFailure) return result.Map(_ => (MembresiaResponse)null!);
        //return Result<MembresiaResponse>.Success(
        //    new MembresiaResponse(result.Value, "Membresía renovada exitosamente.", null, null, null, null));
        return Result<MembresiaResponse>.Success(
        new MembresiaResponse
        {
            MembershipId = result.Value,
            Message = "Membresía renovada exitosamente.",
            TypeName = null,
            StartDate = null,
            EndDate = null,
            DaysUntilExpiry = null
        });

    }
}

// ─────────────────────────────────────────────────────────────────────────────
// CambiarPlanHandler
// ─────────────────────────────────────────────────────────────────────────────
public sealed class CambiarPlanHandler(IMembresiaRepository repo, IValidator<CambiarPlanRequest> validator)
{
    public async Task<Result<MembresiaResponse>> HandleAsync(
        CambiarPlanRequest request, int? userId, string? ip, string? session, CancellationToken ct = default)
    {
        var val = await validator.ValidateAsync(request, ct);
        if (!val.IsValid)
        {
            var errors = val.Errors.GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            return Result<MembresiaResponse>.Failure(new ResultError(ErrorCodes.ErrorValidacion, "Errores de validación.", 422, errors));
        }

        var result = await repo.CambiarPlanAsync(new CambiarPlanParams(
            request.AffiliateId, request.NewMembershipTypeId,
            request.BranchId, request.StartDate, userId, ip, session), ct);

        if (result.IsFailure) return result.Map(_ => (MembresiaResponse)null!);
        //return Result<MembresiaResponse>.Success(
        //    new MembresiaResponse(result.Value, "Plan cambiado exitosamente.", null, null, null, null));
        return Result<MembresiaResponse>.Success(
        new MembresiaResponse
        {
            MembershipId = result.Value,
            Message = "Plan cambiado exitosamente.",
            TypeName = null,
            StartDate = null,
            EndDate = null,
            DaysUntilExpiry = null
        });
    }
}


// ─────────────────────────────────────────────────────────────────────────────
// ListarTiposMembresiaHandler
// ─────────────────────────────────────────────────────────────────────────────
public sealed class ListarTiposMembresiaHandler(IMembresiaRepository repo)
{
    public async Task<Result<IEnumerable<MembershipTypeResponse>>> HandleAsync(CancellationToken ct = default)
    {
        var result = await repo.ListarTiposAsync(ct);
        if (!result.IsSuccess)
            return Result<IEnumerable<MembershipTypeResponse>>.Failure(result.Error!);

        var mapped = result.Value!.Select(r => new MembershipTypeResponse
        {
            MembershipTypeId = r.MembershipTypeId,
            Code = r.Code,
            Name = r.Name,
            DurationDays = r.DurationDays,
            Price = r.Price,
            AccessScope = r.AccessScope,
        });

        return Result<IEnumerable<MembershipTypeResponse>>.Success(mapped);
    }
}

