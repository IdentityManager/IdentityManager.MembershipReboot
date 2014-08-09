using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Thinktecture.IdentityManager;

namespace Thinktecture.IdentityManager.MembershipReboot
{
    public class MembershipRebootPropertyMetadata
    {
        static readonly PropertyMetadata EmailProperty = new PropertyMetadata
        {
            DataType = PropertyDataType.Email,
            Name = "Email",
            Type = Constants.ClaimTypes.Email
        };
    }
}
