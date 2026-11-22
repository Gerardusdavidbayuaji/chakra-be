namespace Chakra.Application.Common;

// Request yang hanya boleh dijalankan admin. Turunan IAuthorizedRequest, jadi cek login tetap jalan lebih dulu.
public interface IAdminRequest<out TResponse> : IAuthorizedRequest<TResponse>;
