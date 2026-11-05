namespace SMS.Application.Output;

public abstract record Yield(int Code);

public record YieldOk<T>(int Code, T Value) : Yield(Code);

public record YieldError(int Code, string Error, string ErrorDescription) : Yield(Code);

public record YieldErrors(int Code, string Error, IEnumerable<string> Errors) : Yield(Code);