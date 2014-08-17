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
    public class CustomDatabase : MembershipRebootDbContext<CustomUser, CustomGroup>
    {
        static CustomDatabase()
        {
            System.Data.Entity.Database.SetInitializer(new System.Data.Entity.DropCreateDatabaseIfModelChanges<CustomDatabase>());
        }

        public CustomDatabase()
            : this("CustomMembershipReboot")
        {
        }

        public CustomDatabase(string name)
            :base(name)
        {
        }
    }
}