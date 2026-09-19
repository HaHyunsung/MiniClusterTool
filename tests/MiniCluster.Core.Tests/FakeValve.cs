using Hhs.Automation.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace MiniCluster.Core.Tests
{
    public class FakeValve : IValve
    {
        #region Fields
        private TimeSpan workingTime;    // ms
        private TimeSpan timeoutTime;    // ms
        #endregion

        #region Construtor
        public FakeValve(string name, TimeSpan workingTime, TimeSpan timeoutTime)
        {
            Name = name;
            this.workingTime = workingTime;
            this.timeoutTime = timeoutTime;
        }
        #endregion

        #region Properties
        public string Name { get; } = "Not Defined";

        public bool Error { get; }

        public int ErrorID { get; }

        public bool RespondsToCommand { get; set; } = true;   // 테스트 중간에 끌 수 있음

        public Permissive OpenPermissive { get; }

        public Permissive ClosePermissive { get; }

        public ValveState State { get; }
        #endregion

        #region Methods
        public Task<bool> OpenAsync(CancellationToken ct)
        {
            if (!OpenPermissive.IsAllowed)
                return Task.FromResult(false);

            return Task.FromResult(true);
        }

        public Task<bool> CloseAsync(CancellationToken ct)
        {
            if (!OpenPermissive.IsAllowed)
                return Task.FromResult(false);

            return Task.FromResult(true);
        }
        #endregion
    }
}
