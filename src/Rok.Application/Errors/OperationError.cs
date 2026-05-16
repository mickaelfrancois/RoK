using CleanArch.DevKit.Mediator.Results;

namespace Rok.Application.Errors;

public sealed record OperationError(string Code, string Message) : Error(Code, Message);
