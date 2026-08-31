using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

namespace MiniCluster.App
{
    public static class AppServices
    {
        public static IServiceCollection ConfigureServices(this IServiceCollection services)
        {
            // 향후 도메인 서비스, 상태 관리자, 뷰모델 등을 여기에 등록함
            return services;
        }
    }
}
