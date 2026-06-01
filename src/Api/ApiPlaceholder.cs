// Este archivo corrige un problema menor del middleware: en GlobalExceptionMiddleware
// se usa `new ApiResponse<object> { }` que no funciona porque las propiedades son init-only.
// La solución es usar serialización directa con un objeto anónimo (ya implementado en el middleware).
// Este archivo es solo referencia — NO MODIFICAR.
// El GlobalExceptionMiddleware ya usa objeto anónimo para la respuesta de error.
namespace GymAffiliate.Api;

/// <summary>
/// Placeholder para evitar namespace vacío.
/// El middleware de excepciones usa objetos anónimos para serializar errores,
/// sin depender de ApiResponse directamente.
/// </summary>
internal static class ApiPlaceholder { }
