using AutoMapper;
using FluentValidation;
using GymAffiliate.Application.DTOs.Requests;
using GymAffiliate.Application.DTOs.Responses;
using GymAffiliate.Domain.Interfaces.Repositories;
using GymAffiliate.Shared.Errors;
using GymAffiliate.Shared.Result;
using Microsoft.Extensions.Logging;

namespace GymAffiliate.Application.UseCases.Afiliados;

// ─────────────────────────────────────────────────────────────────────────────
// CrearAfiliadoHandler
// ─────────────────────────────────────────────────────────────────────────────
public sealed class CrearAfiliadoHandler(
    IAfiliadoRepository repo,
    IValidator<CrearAfiliadoRequest> validator,
    ILogger<CrearAfiliadoHandler> log)
{
    public async Task<Result<CrearAfiliadoResponse>> HandleAsync(
        CrearAfiliadoRequest request,
        int? userId, string? ip, string? session,
        CancellationToken ct = default)
    {
        // 1. Validate
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var errors = validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            return Result<CrearAfiliadoResponse>.Failure(
                new ResultError(ErrorCodes.ErrorValidacion, "Errores de validación.", 422, errors));
        }

        // 2. Call repository
        var result = await repo.CrearAsync(new CrearAfiliadoParams(
            request.DocumentNumber, request.DocumentType,
            request.FirstName, request.LastName, request.BirthDate,
            request.Email, request.Phone, request.Address,
            request.EmergencyContact, request.EmergencyPhone,
            request.BaseBranchId, request.Notes,
            userId, ip, session), ct);

        if (result.IsFailure) return result.Map(_ => (CrearAfiliadoResponse)null!);

        log.LogInformation("Afiliado creado correctamente: Id={Id}", result.Value);
        return Result<CrearAfiliadoResponse>.Success(
            //new CrearAfiliadoResponse(result.Value, "Afiliado registrado exitosamente.")
                new CrearAfiliadoResponse
                {
                    AffiliateId = result.Value,
                    Message = "Afiliado registrado exitosamente."
                }
            );
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// ActualizarAfiliadoHandler
// ─────────────────────────────────────────────────────────────────────────────
public sealed class ActualizarAfiliadoHandler(
    IAfiliadoRepository repo,
    IValidator<ActualizarAfiliadoRequest> validator)
{
    public async Task<Result<int>> HandleAsync(
        int affiliateId, ActualizarAfiliadoRequest request,
        int? userId, string? ip, string? session,
        CancellationToken ct = default)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var errors = validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            return Result<int>.Failure(
                new ResultError(ErrorCodes.ErrorValidacion, "Errores de validación.", 422, errors));
        }

        return await repo.ActualizarAsync(new ActualizarAfiliadoParams(
            affiliateId,
            request.FirstName, request.LastName, request.BirthDate,
            request.Email, request.Phone, request.Address,
            request.EmergencyContact, request.EmergencyPhone,
            request.BaseBranchId, request.Notes,
            userId, ip, session), ct);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// EliminarAfiliadoHandler
// ─────────────────────────────────────────────────────────────────────────────
public sealed class EliminarAfiliadoHandler(IAfiliadoRepository repo)
{
    public Task<GymAffiliate.Shared.Result.Result> HandleAsync(
        int affiliateId, string? notes,
        int? userId, string? ip, string? session,
        CancellationToken ct = default) =>
        repo.EliminarAsync(affiliateId, notes, userId, ip, session, ct);
}

// ─────────────────────────────────────────────────────────────────────────────
// ObtenerAfiliadoHandler
// ─────────────────────────────────────────────────────────────────────────────
public sealed class ObtenerAfiliadoHandler(IAfiliadoRepository repo, IMapper mapper)
{
    public async Task<Result<AfiliadoResponse>> HandleAsync(
        int? id, string? doc, string? email,
        int? userId, CancellationToken ct = default)
    {
        var result = await repo.ObtenerAsync(id, doc, email, userId, ct);
        if (result.IsFailure) return result.Map(_ => (AfiliadoResponse)null!);
        if (result.Value is null)
            return Result<AfiliadoResponse>.Failure(ErrorCodes.AfiliadoNoEncontrado, "Afiliado no encontrado.", 404);

        return Result<AfiliadoResponse>.Success(mapper.Map<AfiliadoResponse>(result.Value));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// ListarAfiliadosHandler
// ─────────────────────────────────────────────────────────────────────────────
public sealed class ListarAfiliadosHandler(IAfiliadoRepository repo, IMapper mapper)
{
    public async Task<Result<(IEnumerable<AfiliadoListaResponse> Items, int Total)>> HandleAsync(
        ListarAfiliadosRequest request, int? userId, CancellationToken ct = default)
    {
        var result = await repo.ListarAsync(new ListarAfiliadosParams(
            request.FilterStatus, request.FilterBranchId, request.FilterSearch,
            request.PageNumber, request.PageSize, userId), ct);

        if (result.IsFailure)
            return Result<(IEnumerable<AfiliadoListaResponse>, int)>.Failure(result.Error!);

        var (items, total) = result.Value;
        var mapped = mapper.Map<IEnumerable<AfiliadoListaResponse>>(items);
        return Result<(IEnumerable<AfiliadoListaResponse>, int)>.Success((mapped, total));
    }
}
