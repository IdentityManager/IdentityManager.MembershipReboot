/*
 * Copyright (c) Dominick Baier, Brock Allen.  All rights reserved.
 * see license
 */

using Owin;

namespace Thinktecture.IdentityManager.Host
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            var factory = new Thinktecture.IdentityManager.Host.MembershipRebootIdentityManagerFactory("CustomMembershipReboot");

            app.UseIdentityManager(new IdentityManagerConfiguration()
            {
                IdentityManagerFactory = factory.Create
            });
        }
    }
}