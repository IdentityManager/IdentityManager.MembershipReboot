﻿using BrockAllen.MembershipReboot;
using BrockAllen.MembershipReboot.Ef;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Thinktecture.IdentityManager.Core;
using Thinktecture.IdentityManager.MembershipReboot;

namespace Thinktecture.IdentityManager.Host
{
    public class MembershipRebootIdentityManagerFactory
    {
        static MembershipRebootConfiguration config;
        static MembershipRebootIdentityManagerFactory()
        {
            System.Data.Entity.Database.SetInitializer(new System.Data.Entity.MigrateDatabaseToLatestVersion<DefaultMembershipRebootDatabase, BrockAllen.MembershipReboot.Ef.Migrations.Configuration>());

            config = new MembershipRebootConfiguration();
            config.PasswordHashingIterationCount = 10000;
            config.RequireAccountVerification = false;
        }

        string connString;
        public MembershipRebootIdentityManagerFactory(string connString)
        {
            this.connString = connString;
        }
        
        public IIdentityManagerService Create()
        {
            var repo = new DefaultUserAccountRepository(this.connString);
            repo.QueryFilter = RelationalUserAccountQuery.Filter;
            repo.QuerySort = RelationalUserAccountQuery.Sort;
            var svc = new UserAccountService(config, repo);
            return new IdentityManagerService<UserAccount>(svc, repo, repo);
        }
    }
}