using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EDISONMAIN2.Services
{
    public record ConnectionParams(
        string Server,
        int RestPort,
        string UserName,
        string FullName,
        int Mlp);
}
