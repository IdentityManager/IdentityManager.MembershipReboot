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
    public class CustomUser : RelationalUserAccount
    {
        [Display(Name="First Name")]
        public string FirstName { get; set; }
        [Display(Name = "Last Name")]
        public string LastName { get; set; }
        public int? Age { get; set; }
    }

    public class CustomDatabase : MembershipRebootDbContext<CustomUser>
    {
        public CustomDatabase()
            : this("CustomMembershipReboot")
        {
        }
        public CustomDatabase(string name)
            :base(name)
        {
        }
    }

    public class MembershipRebootIdentityManagerFactory
    {
        static MembershipRebootConfiguration<CustomUser> config;
        static MembershipRebootIdentityManagerFactory()
        {
            System.Data.Entity.Database.SetInitializer(new System.Data.Entity.DropCreateDatabaseIfModelChanges<CustomDatabase>());

            config = new MembershipRebootConfiguration<CustomUser>();
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
            var db = new CustomDatabase("CustomMembershipReboot");
            var repo = new DbContextUserAccountRepository<CustomDatabase, CustomUser>(db);
            repo.QueryFilter = RelationalUserAccountQuery<CustomUser>.Filter;
            repo.QuerySort = RelationalUserAccountQuery<CustomUser>.Sort;
            var svc = new UserAccountService<CustomUser>(config, repo);

            var idMgr = new MembershipRebootIdentityManagerService<CustomUser>(svc, repo);
            return new DisposableIdentityManagerService(idMgr, db);
        }
    }
}