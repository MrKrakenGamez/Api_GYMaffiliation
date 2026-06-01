namespace GymAffiliate.Domain.Enums;

public enum EstadoAfiliado  { Inactivo = 0, Activo = 1, Vencido = 2, Suspendido = 3 }
public enum EstadoMembresia { Activa = 1, Vencida = 2, Cancelada = 3 }
public enum MetodoPago      { Efectivo = 1, TarjetaCredito = 2, TarjetaDebito = 3, Transferencia = 4, MonederoDigital = 5 }
public enum EstadoPago      { Confirmado = 1, Pendiente = 2, Cancelado = 3, Reembolsado = 4 }
public enum TipoAcceso      { SucursalUnica = 0, TodasSucursales = 1 }
public enum EstadoNotificacion { Pendiente = 1, Enviada = 2, Fallida = 3 }
