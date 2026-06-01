using AutoMapper;
using GymAffiliate.Application.DTOs.Responses;
using GymAffiliate.Domain.Interfaces.Repositories;

namespace GymAffiliate.Application.Common.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // ── Afiliado ──────────────────────────────────────────────────────
        CreateMap<AfiliadoDetalleRaw, AfiliadoResponse>()
            .ForMember(d => d.FullName, o => o.MapFrom(s => $"{s.FirstName} {s.LastName}"))
            .ForMember(d => d.MembresiaVigente, o => o.MapFrom(s =>
                s.MembershipId.HasValue
                    ? new MembresiaResumenResponse
                    {
                        MembershipId = s.MembershipId.Value,
                        TypeCode = s.TypeCode,
                        TypeName = s.MembershipTypeName,
                        AccessScope = s.AccessScope,
                        StartDate = s.StartDate,
                        EndDate = s.EndDate,
                        DaysUntilExpiry = s.DaysUntilExpiry,
                        BranchName = s.MembershipBranchName,
                        RenewalCount = s.RenewalCount
                    }
                    : null))
            .ForMember(d => d.UltimoPago, o => o.MapFrom(s =>
                s.LastPaymentId.HasValue
                    ? new PagoResumenResponse
                    {
                        LastPaymentId = s.LastPaymentId,
                        Amount = s.LastPaymentAmount,
                        PaymentDate = s.LastPaymentDate,
                        MethodName = s.LastPaymentMethod
                    }
                    : null));

        CreateMap<AfiliadoListaRaw, AfiliadoListaResponse>();

        // ── Membresía ─────────────────────────────────────────────────────
        CreateMap<ValidacionAccesoRaw, ValidacionAccesoResponse>();

        // ── Pago ──────────────────────────────────────────────────────────
        CreateMap<PagoListaRaw, PagoResponse>();

        // ── CheckIn ───────────────────────────────────────────────────────
        CreateMap<CheckInRaw, CheckInResponse>()
            .ForMember(d => d.Message, o => o.MapFrom(s =>
                s.AccessGranted
                    ? $"¡Bienvenido, {s.AffiliateName}! Acceso permitido."
                    : s.DenialReason ?? "Acceso denegado."));

        // ── Notificaciones ────────────────────────────────────────────────
        CreateMap<VencimientoRaw, VencimientoResponse>();
        CreateMap<NotificacionPendienteRaw, NotificacionPendienteResponse>();

        // ── Sucursal ──────────────────────────────────────────────────────
        CreateMap<SucursalRaw, SucursalResponse>();

        // ── Reportes ──────────────────────────────────────────────────────
        CreateMap<IngresoMensualRaw, IngresoMensualResponse>();
        CreateMap<AfiliadoEstadoRaw, AfiliadoEstadoResponse>();
    }
}
