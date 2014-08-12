using BrockAllen.MembershipReboot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Thinktecture.IdentityManager.Host
{
    public class MRConfig
    {
        public static readonly MembershipRebootConfiguration<CustomUser> config;
        static MRConfig()
        {
            config = new MembershipRebootConfiguration<CustomUser>();
            config.PasswordHashingIterationCount = 10000;
            config.RequireAccountVerification = false;
            //config.EmailIsUsername = true;
        }
    }
}