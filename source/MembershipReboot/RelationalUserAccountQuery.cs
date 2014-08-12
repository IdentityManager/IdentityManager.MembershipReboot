using BrockAllen.MembershipReboot.Relational;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Thinktecture.IdentityManager;

namespace Thinktecture.IdentityManager.MembershipReboot
{
    public class RelationalUserAccountQuery<TAccount>
        where TAccount : RelationalUserAccount
    {
        public static string NameClaimType = Constants.ClaimTypes.Name;

        public static IQueryable<TAccount> Filter(IQueryable<TAccount> query, string filter)
        {
            return
                from acct in query
                let claims = (from claim in acct.ClaimCollection
                              where claim.Type == NameClaimType && claim.Value.Contains(filter)
                              select claim)
                where
                    acct.Username.Contains(filter) || claims.Any()
                select acct;
        }

        public static IQueryable<TAccount> Sort(IQueryable<TAccount> query)
        {
            return
                from acct in query
                let display = (from claim in acct.ClaimCollection
                               where claim.Type == NameClaimType
                               select claim.Value).FirstOrDefault()
                orderby display ?? acct.Username
                select acct;
        }
    }
}