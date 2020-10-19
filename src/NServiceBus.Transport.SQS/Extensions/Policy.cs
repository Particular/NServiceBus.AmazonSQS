namespace NServiceBus.Transport.SQS.Extensions
{
    using System;
    using System.Collections.Generic;

    class Policy
    {
        public string Id { get; set; }

        public List<Statement> Statements { get; set; } = new List<Statement>();
    }
    class Statement
    {
        public Statement()
        {
        }

        public Statement(StatementEffect effect)
        {
            Effect = effect;
            Id = Guid.NewGuid().ToString().Replace("-", "");
        }

        public enum StatementEffect
        {
            Allow,
            Deny
        }

        public string Id { get; set; }

        public StatementEffect Effect { get; set; }

        public List<ActionIdentifier> Actions { get; set; } = new List<ActionIdentifier>();
        public List<Resource> Resources { get; set; } = new List<Resource>();
        public List<Condition> Conditions { get; set; } = new List<Condition>();
        public List<Principal> Principals { get; set; } = new List<Principal>();
    }

    class ActionIdentifier
    {
        public string ActionName { get; set; }
    }

    class Condition
    {
        public string Type { get; set; }
        public string ConditionKey { get; set; }
        public string[] Values { get; set; } = Array.Empty<string>();
    }

    class Principal
    {
        public string Id { get; set; }
        public string Provider { get; set; }
    }

    class Resource
    {
        public string Id { get; set; }
    }
}