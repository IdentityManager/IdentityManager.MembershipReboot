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
            var repo = new DbContextUserAccountRepository<CustomDatabase, CustomUser>(db);
            repo.QueryFilter = RelationalUserAccountQuery<CustomUser>.Filter;
            repo.QuerySort = RelationalUserAccountQuery<CustomUser>.Sort;
            var svc = new UserAccountService<CustomUser>(MRConfig.config, repo);

            var idMgr = new MembershipRebootIdentityManagerService<CustomUser>(svc, repo);
            return new DisposableIdentityManagerService(idMgr, db);
        }
    }
}