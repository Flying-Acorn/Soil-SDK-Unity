// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedMemberInSuper.Global
#pragma warning disable IDE1006

using System.Collections.Generic;

namespace FlyingAcorn.Soil.Economy.Models.Responses
{
    public class EconomyError
    {
        public string detail { get; set; }
        public Errors errors { get; set; }

        public string GetFullErrorMessage()
        {
            var sb = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(detail))
            {
                sb.Append(detail);
            }

            if (errors != null)
            {
                void AppendErrors(string fieldName, List<string> fieldErrors)
                {
                    if (fieldErrors != null && fieldErrors.Count > 0)
                    {
                        if (sb.Length > 0) sb.Append("\n");
                        sb.Append($"{fieldName}: {string.Join(", ", fieldErrors)}");
                    }
                }

                AppendErrors("Balance", errors.balance);
                AppendErrors("Name", errors.name);
                AppendErrors("Identifier", errors.identifier);
                AppendErrors("Amount", errors.amount);
            }

            return sb.Length > 0 ? sb.ToString() : "Unknown Economy Error";
        }
    }

    public class Errors
    {
        public List<string> balance;
        public List<string> name;
        public List<string> identifier;
        public List<string> amount;
    }
}
