namespace EDISONMAIN2;

public record LoginResult(
    int Mlp,
    string UserName,
    string FullName,
    bool IsAdmin,
    string Directory,
    bool ChangePasswordNeeded,
    bool MayUpdate,
    string Email);
