using BrockAllen.MembershipReboot;
using BrockAllen.MembershipReboot.Ef;
using BrockAllen.MembershipReboot.Relational;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
using Thinktecture.IdentityManager;
using Thinktecture.IdentityManager.MembershipReboot;

namespace Thinktecture.IdentityManager.Host
{
    public class MembershipRebootIdentityManagerFactory
    {
        string connString;
        public MembershipRebootIdentityManagerFactory(string connString)
        {
            this.connString = connString;
        }
        
        public IIdentityManagerService Create()
        {
            var db = new CustomDatabase(connString);
            var userRepo = new DbContextUserAccountRepository<CustomDatabase, CustomUser>(db);
            userRepo.QueryFilter = RelationalUserAccountQuery<CustomUser>.Filter;
            userRepo.QuerySort = RelationalUserAccountQuery<CustomUser>.Sort;
            var userSvc = new UserAccountService<CustomUser>(MRConfig.config, userRepo);

            var groupRepo = new DbContextGroupRepository<CustomDatabase, CustomGroup>(db);
            var groupSvc = new GroupService<CustomGroup>(MRConfig.config.DefaultTenant, groupRepo);

            var idMgr = new MembershipRebootIdentityManagerService<CustomUser, CustomGroup>(userSvc, userRepo, groupSvc, groupRepo);
            return new DisposableIdentityManagerService(idMgr, db);
        }
    }
}