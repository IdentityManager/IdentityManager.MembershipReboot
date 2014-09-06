using BrockAllen.MembershipReboot;
using BrockAllen.MembershipReboot.Ef;
using BrockAllen.MembershipReboot.Relational;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
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

            MembershipRebootIdentityManagerService<CustomUser, CustomGroup> idMgr = null;
            idMgr = new MembershipRebootIdentityManagerService<CustomUser, CustomGroup>(userSvc, userRepo, groupSvc, groupRepo);
            
            // uncomment to allow additional properties mapped to claims
            //idMgr = new MembershipRebootIdentityManagerService<CustomUser, CustomGroup>(userSvc, userRepo, groupSvc, groupRepo, () =>
            //{
            //    var meta = idMgr.GetStandardMetadata();
            //    meta.UserMetadata.UpdateProperties =
            //        meta.UserMetadata.UpdateProperties.Union(
            //            new PropertyMetadata[] { 
            //                idMgr.GetMetadataForClaim(Constants.ClaimTypes.Name, "Name")
            //            }
            //        );
            //    return Task.FromResult(meta);
            //});

            return new DisposableIdentityManagerService(idMgr, db);
        }
    }
}