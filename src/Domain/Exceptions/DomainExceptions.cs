namespace GymAffiliate.Domain.Exceptions;

public class DomainException(string message) : Exception(message);

public class NotFoundException(string entity, object id)
    : DomainException($"{entity} con id '{id}' no fue encontrado.");

public class BusinessRuleException(string code, string message) : DomainException(message)
{
    public string ErrorCode { get; } = code;
}

public class UnauthorizedException(string message = "No autorizado.")
    : DomainException(message);

public class ValidationException(Dictionary<string, string[]> errors)
    : DomainException("Errores de validacion.")
{
    public Dictionary<string, string[]> Errors { get; } = errors;
}
