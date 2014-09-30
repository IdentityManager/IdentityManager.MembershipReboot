﻿/*
 * Copyright 2014 Dominick Baier, Brock Allen
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
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