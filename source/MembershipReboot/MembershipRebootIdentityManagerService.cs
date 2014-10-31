/*
 * Copyright 2014 Dominick Baier, Brock Allen
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using BrockAllen.MembershipReboot;
using BrockAllen.MembershipReboot.Relational;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Thinktecture.IdentityManager;

namespace Thinktecture.IdentityManager.MembershipReboot
{
    public class MembershipRebootIdentityManagerService<TAccount, TGroup> : IIdentityManagerService
        where TAccount : UserAccount, new()
        where TGroup : Group, new()
    {
        readonly UserAccountService<TAccount> userAccountService;
        readonly IUserAccountQuery<TAccount> userQuery;
        readonly GroupService<TGroup> groupService;
        readonly IGroupQuery groupQuery;
        readonly Func<Task<IdentityManagerMetadata>> metadataFunc;

        public Func<IQueryable<TAccount>, string, IQueryable<TAccount>> Filter { get; set; }
        public Func<IQueryable<TAccount>, IQueryable<TAccount>> Sort { get; set; }

        public MembershipRebootIdentityManagerService(
           UserAccountService<TAccount> userAccountService,
           GroupService<TGroup> groupService,
           bool includeAccountProperties = true)
            : this(userAccountService, userAccountService.Query, groupService, groupService.Query, includeAccountProperties)
        {
        }

        public MembershipRebootIdentityManagerService(
            UserAccountService<TAccount> userAccountService,
            IUserAccountQuery<TAccount> userQuery,
            GroupService<TGroup> groupService,
            IGroupQuery groupQuery,
            bool includeAccountProperties = true)
        {
            if (userAccountService == null) throw new ArgumentNullException("userAccountService");
            if (userQuery == null) throw new ArgumentNullException("userQuery");

            this.userAccountService = userAccountService;
            this.userQuery = userQuery;

            this.groupService = groupService;
            this.groupQuery = groupQuery;

            this.metadataFunc = ()=>Task.FromResult(GetStandardMetadata(includeAccountProperties));

            if (typeof(RelationalUserAccount).IsAssignableFrom(typeof(TAccount)))
            {
                this.Filter = RelationalUserAccountQuery<TAccount>.DefaultFilter;
                this.Sort = RelationalUserAccountQuery<TAccount>.DefaultSort;
            }
            else
            {
                this.Filter = DefaultFilter;
                this.Sort = DefaultSort;
            }
        }
        
        public MembershipRebootIdentityManagerService(
            UserAccountService<TAccount> userAccountService,
            IUserAccountQuery<TAccount> userQuery,
            GroupService<TGroup> groupService,
            IGroupQuery groupQuery,
            IdentityManagerMetadata metadata)
            : this(userAccountService, userQuery, groupService, groupQuery, ()=>Task.FromResult(metadata))
        {
        }

        public MembershipRebootIdentityManagerService(
            UserAccountService<TAccount> userAccountService,
            IUserAccountQuery<TAccount> userQuery,
            GroupService<TGroup> groupService,
            IGroupQuery groupQuery,
            Func<Task<IdentityManagerMetadata>> metadataFunc)
            : this(userAccountService, userQuery, groupService, groupQuery)
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

            if (this.groupService != null && this.groupQuery != null)
            {
                meta.RoleMetadata.SupportsCreate = true;
                meta.RoleMetadata.SupportsDelete = true;
                meta.RoleMetadata.RoleClaimType = Constants.ClaimTypes.Role;
                meta.RoleMetadata.CreateProperties = new PropertyMetadata[]{
                    new PropertyMetadata{
                        Name = "Name",
                        Type = Constants.ClaimTypes.Name,
                        DataType = PropertyDataType.String,
                        Required = true
                    }
                };
            }

            return meta;
        }

        public PropertyMetadata GetMetadataForClaim(string type, string name = null, PropertyDataType dataType = PropertyDataType.String, bool required = false)
        {
            return PropertyMetadata.FromFunctions<TAccount, string>(type, GetForClaim(type), SetForClaim(type), name, dataType, required);
        }
        public Func<TAccount, string> GetForClaim(string type)
        {
            return account => account.Claims.Where(x => x.Type == type).Select(x => x.Value).FirstOrDefault();
        }
        public Action<TAccount, string> SetForClaim(string type)
        {
            return (account, value) =>
            {
                this.userAccountService.RemoveClaim(account.ID, type);
                if (!String.IsNullOrWhiteSpace(value))
                {
                    this.userAccountService.AddClaim(account.ID, type, value);
                }
            };
        }

        public string GetUsername(TAccount account)
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
        
        public void SetUsername(TAccount account, string username)
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

        public void SetPassword(TAccount account, string password)
        {
            this.userAccountService.SetPassword(account.ID, password);
        }

        public string GetEmail(TAccount account)
        {
            return account.Email;
        }
        public void SetConfirmedEmail(TAccount account, string email)
        {
            this.userAccountService.SetConfirmedEmail(account.ID, email);
        }

        public string GetPhone(TAccount account)
        {
            return account.MobilePhoneNumber;
        }
        public void SetConfirmedPhone(TAccount account, string phone)
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

        public bool GetIsLoginAllowed(TAccount account)
        {
            return account.IsLoginAllowed;
        }
        public void SetIsLoginAllowed(TAccount account, bool value)
        {
            this.userAccountService.SetIsLoginAllowed(account.ID, value);
        }

        public Task<IdentityManagerMetadata> GetMetadataAsync()
        {
            return this.metadataFunc();
        }

        protected virtual IQueryable<TAccount> DefaultFilter(IQueryable<TAccount> query, string filter)
        {
            return
                from acct in query
                where acct.Username.Contains(filter)
                select acct;
        }

        protected virtual IQueryable<TAccount> DefaultSort(IQueryable<TAccount> query)
        {
            var result =
                from acct in query
                orderby acct.Username
                select acct;
            return result;
        }

        public Task<IdentityManagerResult<QueryResult<UserSummary>>> QueryUsersAsync(string filter, int start, int count)
        {
            if (start < 0) start = 0;
            if (count < 0) count = Int32.MaxValue;
            
            var filterFunc = this.Filter;
            if (String.IsNullOrWhiteSpace(filter))
            {
                filterFunc = (q, f) => q;
            }
            
            int total;
            var users = userQuery.Query(query => filterFunc(query, filter), Sort, start, count, out total).ToArray();

            var result = new QueryResult<UserSummary>();
            result.Start = start;
            result.Count = count;
            result.Total = total;
            result.Filter = filter;
            result.Items = users.Select(x =>
            {
                var user = new UserSummary
                {
                    Subject = x.ID.ToString("D"),
                    Username = x.Username,
                    Name = DisplayNameFromUserId(x.ID)
                };
                
                return user;
            }).ToArray();

            return Task.FromResult(new IdentityManagerResult<QueryResult<UserSummary>>(result));
        }

        string DisplayNameFromUserId(Guid id)
        {
            var acct = userAccountService.GetByID(id);
            return acct.Claims.Where(x=>x.Type == Constants.ClaimTypes.Name).Select(x=>x.Value).FirstOrDefault();
        }

        public async Task<IdentityManagerResult<CreateResult>> CreateUserAsync(IEnumerable<Thinktecture.IdentityManager.PropertyValue> properties)
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
                var createProps = metadata.UserMetadata.GetCreateProperties();

                var acct = new TAccount();
                foreach (var prop in otherProperties)
                {
                    SetUserProperty(createProps, acct, prop.Type, prop.Value);
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

                var props = new List<PropertyValue>();
                foreach(var prop in metadata.UserMetadata.UpdateProperties)
                {
                    props.Add(new PropertyValue
                    {
                        Type = prop.Type, 
                        Value = GetUserProperty(prop, acct)
                    });
                }
                user.Properties = props.ToArray();

                var claims = new List<Thinktecture.IdentityManager.ClaimValue>();
                if (acct.Claims != null)
                {
                    claims.AddRange(acct.Claims.Select(x => new Thinktecture.IdentityManager.ClaimValue { Type = x.Type, Value = x.Value }));
                }
                user.Claims = claims.ToArray();

                return new IdentityManagerResult<UserDetail>(user);
            }
            catch (ValidationException ex)
            {
                return new IdentityManagerResult<UserDetail>(ex.Message);
            }
        }

        public async Task<IdentityManagerResult> SetUserPropertyAsync(string subject, string type, string value)
        {
            Guid g;
            if (!Guid.TryParse(subject, out g))
            {
                return new IdentityManagerResult("Invalid subject");
            }

            try
            {
                var acct = this.userAccountService.GetByID(g);
                if (acct == null)
                {
                    return new IdentityManagerResult("Invalid subject");
                }

                var errors = ValidateUserProperty(type, value);
                if (errors.Any())
                {
                    return new IdentityManagerResult(errors.ToArray());
                }

                var metadata = await GetMetadataAsync();
                SetUserProperty(metadata.UserMetadata.UpdateProperties, acct, type, value);
                userAccountService.Update(acct);

                return IdentityManagerResult.Success;
            }
            catch (ValidationException ex)
            {
                return new IdentityManagerResult(ex.Message);
            }
        }

        public Task<IdentityManagerResult> AddUserClaimAsync(string subject, string type, string value)
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

        public Task<IdentityManagerResult> RemoveUserClaimAsync(string subject, string type, string value)
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

        IEnumerable<string> ValidateUserProperties(IEnumerable<UserClaim> properties)
        {
            return properties.Select(x => ValidateUserProperty(x.Type, x.Value)).Aggregate((x, y) => x.Concat(y));
        }
        
        private IEnumerable<string> ValidateUserProperty(string type, string value)
        {
            return Enumerable.Empty<string>();
        }

        private string GetUserProperty(PropertyMetadata propMetadata, TAccount user)
        {
            string val;
            if (propMetadata.TryGet(user, out val))
            {
                return val;
            }

            throw new Exception("Invalid property type " + propMetadata.Type);
        }

        private void SetUserProperty(IEnumerable<PropertyMetadata> propsMeta, TAccount user, string type, string value)
        {
            if (propsMeta.TrySet(user, type, value))
            {
                return;
            }

            throw new Exception("Invalid property type " + type);
        }

        void ValidateSupportsGroups()
        {
            if (groupService == null || groupQuery == null)
            {
                throw new InvalidOperationException("Groups Not Supported");
            }
        }

        public async Task<IdentityManagerResult<CreateResult>> CreateRoleAsync(IEnumerable<PropertyValue> properties)
        {
            ValidateSupportsGroups();

            var nameClaim = properties.Single(x => x.Type == Constants.ClaimTypes.Name);

            var name = nameClaim.Value;

            string[] exclude = new string[] { Constants.ClaimTypes.Name };
            var otherProperties = properties.Where(x => !exclude.Contains(x.Type)).ToArray();

            try
            {
                var metadata = await GetMetadataAsync();
                var createProps = metadata.RoleMetadata.GetCreateProperties();

                // TODO support properties on groups
                var group = this.groupService.Create(name);
                //foreach (var prop in otherProperties)
                //{
                //    SetGroupProperty(createProps, group, prop.Type, prop.Value);
                //}
                //this.groupService.


                return new IdentityManagerResult<CreateResult>(new CreateResult { Subject = group.ID.ToString("D") });
            }
            catch (ValidationException ex)
            {
                return new IdentityManagerResult<CreateResult>(ex.Message);
            }
        }

        public Task<IdentityManagerResult> DeleteRoleAsync(string subject)
        {
            ValidateSupportsGroups();

            Guid g;
            if (!Guid.TryParse(subject, out g))
            {
                return Task.FromResult(new IdentityManagerResult("Invalid subject"));
            }

            try
            {
                this.groupService.Delete(g);
            }
            catch (ValidationException ex)
            {
                return Task.FromResult(new IdentityManagerResult(ex.Message));
            }

            return Task.FromResult(IdentityManagerResult.Success);
        }

        public async Task<IdentityManagerResult<RoleDetail>> GetRoleAsync(string subject)
        {
            ValidateSupportsGroups();
            
            Guid g;
            if (!Guid.TryParse(subject, out g))
            {
                return new IdentityManagerResult<RoleDetail>("Invalid subject");
            }

            try
            {
                var group = this.groupService.Get(g);
                if (group == null)
                {
                    return new IdentityManagerResult<RoleDetail>((RoleDetail)null);
                }

                var role = new RoleDetail
                {
                    Subject = subject,
                    Name = group.Name,
                    //Description = group.Name
                };

                var metadata = await GetMetadataAsync();

                var props = new List<PropertyValue>();
                foreach (var prop in metadata.RoleMetadata.UpdateProperties)
                {
                    props.Add(new PropertyValue
                    {
                        Type = prop.Type,
                        Value = GetGroupProperty(prop, group)
                    });
                }
                role.Properties = props.ToArray();

                return new IdentityManagerResult<RoleDetail>(role);
            }
            catch (ValidationException ex)
            {
                return new IdentityManagerResult<RoleDetail>(ex.Message);
            }
        }

        public Task<IdentityManagerResult<QueryResult<RoleSummary>>> QueryRolesAsync(string filter, int start, int count)
        {
            ValidateSupportsGroups();

            if (start < 0) start = 0;
            if (count < 0) count = Int32.MaxValue;

            int total;
            var groups = groupQuery.Query(filter, start, count, out total).ToArray();

            var result = new QueryResult<RoleSummary>();
            result.Start = start;
            result.Count = count;
            result.Total = total;
            result.Filter = filter;
            result.Items = groups.Select(x =>
            {
                var role = new RoleSummary
                {
                    Subject = x.ID.ToString("D"),
                    Name = x.Name,
                    //Description = x.Name
                };

                return role;
            }).ToArray();

            return Task.FromResult(new IdentityManagerResult<QueryResult<RoleSummary>>(result));
        }

        public async Task<IdentityManagerResult> SetRolePropertyAsync(string subject, string type, string value)
        {
            ValidateSupportsGroups();

            Guid g;
            if (!Guid.TryParse(subject, out g))
            {
                return new IdentityManagerResult("Invalid subject");
            }

            try
            {
                var group = this.groupService.Get(g);
                if (group == null)
                {
                    return new IdentityManagerResult("Invalid subject");
                }

                var errors = ValidateGroupProperty(type, value);
                if (errors.Any())
                {
                    return new IdentityManagerResult(errors.ToArray());
                }

                var metadata = await GetMetadataAsync();
                SetGroupProperty(metadata.RoleMetadata.UpdateProperties, group, type, value);
                // TODO : support updates on groups
                //groupService.Update(group);

                return IdentityManagerResult.Success;
            }
            catch (ValidationException ex)
            {
                return new IdentityManagerResult(ex.Message);
            }
        }

        IEnumerable<string> ValidateGroupProperties(IEnumerable<PropertyValue> properties)
        {
            return properties.Select(x => ValidateGroupProperty(x.Type, x.Value)).Aggregate((x, y) => x.Concat(y));
        }

        private IEnumerable<string> ValidateGroupProperty(string type, string value)
        {
            return Enumerable.Empty<string>();
        }

        private string GetGroupProperty(PropertyMetadata propMetadata, TGroup group)
        {
            string val;
            if (propMetadata.TryGet(group, out val))
            {
                return val;
            }

            throw new Exception("Invalid property type " + propMetadata.Type);
        }

        private void SetGroupProperty(IEnumerable<PropertyMetadata> propsMeta, TGroup group, string type, string value)
        {
            if (propsMeta.TrySet(group, type, value))
            {
                return;
            }

            throw new Exception("Invalid property type " + type);
        }
    }
}
