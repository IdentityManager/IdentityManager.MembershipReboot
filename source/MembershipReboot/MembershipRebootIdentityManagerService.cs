/*
 * Copyright (c) Dominick Baier, Brock Allen.  All rights reserved.
 * see license
 */
using BrockAllen.MembershipReboot;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Thinktecture.IdentityManager;

namespace Thinktecture.IdentityManager.MembershipReboot
{
    public class MembershipRebootIdentityManagerService<TAccount> : IIdentityManagerService
        where TAccount : UserAccount, new()
    {
        readonly UserAccountService<TAccount> userAccountService;
        readonly IUserAccountQuery query;
        readonly Func<Task<IdentityManagerMetadata>> metadataFunc;

        public MembershipRebootIdentityManagerService(
            UserAccountService<TAccount> userAccountService,
            IUserAccountQuery query,
            bool includeAccountProperties = true)
        {
            if (userAccountService == null) throw new ArgumentNullException("userAccountService");
            if (query == null) throw new ArgumentNullException("query");

            this.userAccountService = userAccountService;
            this.query = query;
            this.metadataFunc = ()=>Task.FromResult(GetStandardMetadata(includeAccountProperties));
        }
        
        public MembershipRebootIdentityManagerService(
            UserAccountService<TAccount> userAccountService, 
            IUserAccountQuery query,
            IdentityManagerMetadata metadata)
            : this(userAccountService, query, ()=>Task.FromResult(metadata))
        {
        }

        public MembershipRebootIdentityManagerService(
            UserAccountService<TAccount> userAccountService,
            IUserAccountQuery query,
            Func<Task<IdentityManagerMetadata>> metadataFunc)
            : this(userAccountService, query)
        {
            if (metadataFunc == null) throw new ArgumentNullException("metadataFunc");
            this.metadataFunc = metadataFunc;
        }

        public IdentityManagerMetadata GetStandardMetadata(bool includeAccountProperties = true)
        {
            var update = new List<PropertyMetadata>();
            if (userAccountService.Configuration.EmailIsUsername)
            {
                update.AddRange(new PropertyMetadata[]{
                    PropertyMetadata.FromFunctions<TAccount, string>(Constants.ClaimTypes.Username, GetUsername, SetUsername, name: "Email", dataType: PropertyDataType.Email, required: true),
                    PropertyMetadata.FromFunctions<TAccount, string>(Constants.ClaimTypes.Password, x => null, SetPassword, name: "Password", dataType: PropertyDataType.Password, required: true),
                });
            }
            else
            {
                update.AddRange(new PropertyMetadata[]{
                    PropertyMetadata.FromFunctions<TAccount, string>(Constants.ClaimTypes.Username, GetUsername, SetUsername, name: "Username", dataType: PropertyDataType.String, required: true),
                    PropertyMetadata.FromFunctions<TAccount, string>(Constants.ClaimTypes.Password, x => null, SetPassword, name: "Password", dataType: PropertyDataType.Password, required: true),
                    PropertyMetadata.FromFunctions<TAccount, string>(Constants.ClaimTypes.Email, GetEmail, SetConfirmedEmail, name: "Email", dataType: PropertyDataType.Email, required: userAccountService.Configuration.RequireAccountVerification),
                });
            }

            var create = new List<PropertyMetadata>();
            if (!userAccountService.Configuration.EmailIsUsername && !userAccountService.Configuration.RequireAccountVerification)
            {
                create.AddRange(update.Where(x=>x.Required).ToArray());
                create.AddRange(new PropertyMetadata[]{
                    PropertyMetadata.FromFunctions<TAccount, string>(Constants.ClaimTypes.Email, GetEmail, SetConfirmedEmail, name: "Email", dataType: PropertyDataType.Email, required: false),
                });
            }

            update.AddRange(new PropertyMetadata[] {
                PropertyMetadata.FromFunctions<TAccount, string>(Constants.ClaimTypes.Phone, GetPhone, SetConfirmedPhone, name: "Phone", dataType: PropertyDataType.String, required: false),
                PropertyMetadata.FromFunctions<TAccount, string>(Constants.ClaimTypes.Name, GetName, SetName, name: "Name", dataType: PropertyDataType.String, required: false),
                PropertyMetadata.FromFunctions<TAccount, bool>("IsLoginAllowed", GetIsLoginAllowed, SetIsLoginAllowed, name: "Is Login Allowed", dataType: PropertyDataType.Boolean, required: false),
            });

            if (includeAccountProperties)
            {
                update.AddRange(PropertyMetadata.FromType<TAccount>());
            }

            var user = new UserMetadata
            {
                SupportsCreate = true,
                SupportsDelete = true,
                SupportsClaims = true,
                CreateProperties = create,
                UpdateProperties = update
            };

            var meta = new IdentityManagerMetadata{
                UserMetadata = user
            };
            return meta;
        }

        protected string GetUsername(TAccount account)
        {
            if (this.userAccountService.Configuration.EmailIsUsername)
            {
                return account.Email;
            }
            else
            {
                return account.Username;
            }
        }
        protected void SetUsername(TAccount account, string username)
        {
            if (this.userAccountService.Configuration.EmailIsUsername)
            {
                userAccountService.SetConfirmedEmail(account.ID, username);
            }
            else
            {
                userAccountService.ChangeUsername(account.ID, username);
            }
        }

        protected void SetPassword(TAccount account, string password)
        {
            this.userAccountService.SetPassword(account.ID, password);
        }

        protected string GetEmail(TAccount account)
        {
            return account.Email;
        }
        protected void SetConfirmedEmail(TAccount account, string email)
        {
            this.userAccountService.SetConfirmedEmail(account.ID, email);
        }

        protected string GetPhone(TAccount account)
        {
            return account.MobilePhoneNumber;
        }
        protected void SetConfirmedPhone(TAccount account, string phone)
        {
            if (String.IsNullOrWhiteSpace(phone))
            {
                this.userAccountService.RemoveMobilePhone(account.ID);
            }
            else
            {
                this.userAccountService.SetConfirmedMobilePhone(account.ID, phone);
            }
        }

        protected bool GetIsLoginAllowed(TAccount account)
        {
            return account.IsLoginAllowed;
        }
        protected void SetIsLoginAllowed(TAccount account, bool value)
        {
            this.userAccountService.SetIsLoginAllowed(account.ID, value);
        }

        protected string GetName(TAccount account)
        {
            return account.Claims.Where(x => x.Type == Constants.ClaimTypes.Name).Select(x => x.Value).FirstOrDefault();
        }
        protected void SetName(TAccount account, string name)
        {
            this.userAccountService.RemoveClaim(account.ID, Constants.ClaimTypes.Name);
            if (!String.IsNullOrWhiteSpace(name))
            {
                this.userAccountService.AddClaim(account.ID, Constants.ClaimTypes.Name, name);
            }
        }
        
        public Task<IdentityManagerMetadata> GetMetadataAsync()
        {
            return this.metadataFunc();
        }

        public Task<IdentityManagerResult<QueryResult>> QueryUsersAsync(string filter, int start, int count)
        {
            int total;
            var users = query.Query(filter, start, count, out total).ToArray();

            var result = new QueryResult();
            result.Start = start;
            result.Count = count;
            result.Total = total;
            result.Filter = filter;
            result.Users = users.Select(x =>
            {
                var user = new UserResult
                {
                    Subject = x.ID.ToString("D"),
                    Username = x.Username,
                    Name = DisplayNameFromUserId(x.ID)
                };
                
                return user;
            }).ToArray();

            return Task.FromResult(new IdentityManagerResult<QueryResult>(result));
        }

        string DisplayNameFromUserId(Guid id)
        {
            var acct = userAccountService.GetByID(id);
            return acct.Claims.Where(x=>x.Type == Constants.ClaimTypes.Name).Select(x=>x.Value).FirstOrDefault();
        }

        public async Task<IdentityManagerResult<CreateResult>> CreateUserAsync(IEnumerable<Thinktecture.IdentityManager.UserClaim> properties)
        {
            var usernameClaim = properties.Single(x => x.Type == Constants.ClaimTypes.Username);
            var passwordClaim = properties.Single(x => x.Type == Constants.ClaimTypes.Password);
            var emailClaim = properties.SingleOrDefault(x => x.Type == Constants.ClaimTypes.Email);

            var username = usernameClaim.Value;
            var password = passwordClaim.Value;
            var email = emailClaim != null ? emailClaim.Value : null;

            string[] exclude = new string[] { Constants.ClaimTypes.Username, Constants.ClaimTypes.Password, Constants.ClaimTypes.Email };
            var otherProperties = properties.Where(x => !exclude.Contains(x.Type)).ToArray();

            try
            {
                var metadata = await GetMetadataAsync();

                var acct = new TAccount();
                foreach (var prop in otherProperties)
                {
                    SetProperty(prop.Type, prop.Value, acct, metadata.UserMetadata);
                }

                if (this.userAccountService.Configuration.EmailIsUsername)
                {
                    acct = this.userAccountService.CreateAccount(null, null, password, username, account:acct);
                }
                else
                {
                    acct = this.userAccountService.CreateAccount(null, username, password, email, account: acct);
                }

                return new IdentityManagerResult<CreateResult>(new CreateResult { Subject = acct.ID.ToString("D") });
            }
            catch (ValidationException ex)
            {
                return new IdentityManagerResult<CreateResult>(ex.Message);
            }
        }
        
        public Task<IdentityManagerResult> DeleteUserAsync(string subject)
        {
            Guid g;
            if (!Guid.TryParse(subject, out g))
            {
                return Task.FromResult(new IdentityManagerResult("Invalid subject"));
            }

            try
            {
                this.userAccountService.DeleteAccount(g);
            }
            catch (ValidationException ex)
            {
                return Task.FromResult(new IdentityManagerResult(ex.Message));
            } 

            return Task.FromResult(IdentityManagerResult.Success);
        }

        public async Task<IdentityManagerResult<UserDetail>> GetUserAsync(string subject)
        {
            Guid g;
            if (!Guid.TryParse(subject, out g))
            {
                return new IdentityManagerResult<UserDetail>("Invalid subject");
            }

            try
            {
                var acct = this.userAccountService.GetByID(g);
                if (acct == null)
                {
                    return new IdentityManagerResult<UserDetail>((UserDetail)null);
                }

                var user = new UserDetail
                {
                    Subject = subject,
                    Username = acct.Username,
                    Name = DisplayNameFromUserId(acct.ID),
                };

                var metadata = await GetMetadataAsync();

                var props = 
                    from prop in metadata.UserMetadata.UpdateProperties
                    select new UserClaim
                    {
                        Type = prop.Type,
                        Value = GetProperty(prop.Type, acct, metadata.UserMetadata)
                    };
                user.Properties = props.ToArray();

                var claims = new List<Thinktecture.IdentityManager.UserClaim>();
                if (acct.Claims != null)
                {
                    claims.AddRange(acct.Claims.Select(x => new Thinktecture.IdentityManager.UserClaim { Type = x.Type, Value = x.Value }));
                }
                user.Claims = claims.ToArray();

                return new IdentityManagerResult<UserDetail>(user);
            }
            catch (ValidationException ex)
            {
                return new IdentityManagerResult<UserDetail>(ex.Message);
            }
        }

        public async Task<IdentityManagerResult> SetPropertyAsync(string subject, string type, string value)
        {
            Guid g;
            if (!Guid.TryParse(subject, out g))
            {
                return new IdentityManagerResult<UserDetail>("Invalid subject");
            }

            try
            {
                var acct = this.userAccountService.GetByID(g);
                if (acct == null)
                {
                    return new IdentityManagerResult<UserDetail>((UserDetail)null);
                }

                var metadata = await GetMetadataAsync();
                SetProperty(type, value, acct, metadata.UserMetadata);
                userAccountService.Update(acct);

                return IdentityManagerResult.Success;
            }
            catch (ValidationException ex)
            {
                return new IdentityManagerResult<UserDetail>(ex.Message);
            }
        }

        public Task<IdentityManagerResult> AddClaimAsync(string subject, string type, string value)
        {
            Guid g;
            if (!Guid.TryParse(subject, out g))
            {
                return Task.FromResult(new IdentityManagerResult("Invalid user."));
            }

            try
            {
                this.userAccountService.AddClaim(g, type, value);
            }
            catch (ValidationException ex)
            {
                return Task.FromResult(new IdentityManagerResult(ex.Message));
            }

            return Task.FromResult(IdentityManagerResult.Success);
        }

        public Task<IdentityManagerResult> RemoveClaimAsync(string subject, string type, string value)
        {
            Guid g;
            if (!Guid.TryParse(subject, out g))
            {
                return Task.FromResult(new IdentityManagerResult("Invalid user."));
            }

            try
            {
                this.userAccountService.RemoveClaim(g, type, value);
            }
            catch (ValidationException ex)
            {
                return Task.FromResult(new IdentityManagerResult(ex.Message));
            }

            return Task.FromResult(IdentityManagerResult.Success);
        }

        private string GetProperty(string type, TAccount user, UserMetadata meta)
        {
            string val;
            if (meta.TryGet(user, type, out val))
            {
                return val;
            }

            throw new Exception("Invalid property type " + type);
        }

        private void SetProperty(string type, string value, TAccount user, UserMetadata meta)
        {
            if (meta.TrySet(user, type, value))
            {
                return;
            }

            throw new Exception("Invalid property type " + type);
        }
    }
}
