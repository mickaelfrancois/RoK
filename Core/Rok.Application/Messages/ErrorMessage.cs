namespace Rok.Application.Messages;

public class ErrorMessage
{
    public string ErrorCode { get; set; }

    public Exception? Exception { get; set; }


    public ErrorMessage(string errorCode)
    {
        ErrorCode = errorCode;
    }

    public ErrorMessage(string errorCode, Exception exception)
    {
        ErrorCode = errorCode;
        Exception = exception;
    }
}