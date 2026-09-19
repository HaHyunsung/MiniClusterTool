using System;
using System.Collections.Generic;
using System.Text;

namespace Hhs.Automation.Core
{
    public sealed class Permissive
    {
        private sealed record Condition(Func<bool> IsMet, string Reason);

        private readonly List<Condition> conditions = new();

        public void AddCondition(Func<bool> isMet, string message)
        {
            conditions.Add(new Condition(isMet, message));
        }


        public bool IsAllowed
        {
            get
            {
                foreach (Condition condition in conditions)
                {
                    if (!condition.IsMet()) return false;
                }
                return true;
            }
        }

        public IReadOnlyList<string> BlockedReasons
        {
            get
            {
                var result = new List<string>();

                foreach (var condition in conditions)
                {
                    if (!condition.IsMet())
                        result.Add(condition.Reason);
                }
                return result;
            }
        }

        public string BlockedReasonsText => string.Join(" | ", BlockedReasons);
    }
}
