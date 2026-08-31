using System;
using System.Collections.Generic;
using System.Text;

namespace MiniCluster.Core
{
    public class DoorController
    {
        private readonly ISensor _sensor;

        public DoorController(ISensor sensor)
        {
            _sensor = sensor;
        }

        public bool CanOpen()
        {
            return _sensor.IsActive();
        }

        // 리뷰 실습용 함수 추가 (3초 설정)
        public int GetTimeoutSeconds()
        {
            return 5;
        }
    }
}
