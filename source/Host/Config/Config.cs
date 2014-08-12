using BrockAllen.MembershipReboot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Thinktecture.IdentityManager.Host
{
    public class Config
    {
        public static readonly MembershipRebootConfiguration<CustomUser> config;
        static Config()
        {
            config = new MembershipRebootConfiguration<CustomUser>();
            config.PasswordHashingIterationCount = 10000;
            config.RequireAccountVerification = false;
        }
    }
}