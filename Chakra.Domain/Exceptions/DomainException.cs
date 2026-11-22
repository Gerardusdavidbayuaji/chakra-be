namespace Chakra.Domain.Exceptions;

// Pelanggaran aturan bisnis. Pesannya aman ditampilkan ke client.
public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }
}
