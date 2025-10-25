// --------------------------------------------------------------------------------------------------------------------
// <copyright>
//   Copyright lestoilfante 2023 (https://github.com/lestoilfante)
//   
//   GNU General Public License version 2.1 (GPLv2.1) 
// </copyright>
// --------------------------------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;
using System.Linq;

namespace Omni2FA.NPS.Adapter.Utils {
    /// <summary>
    /// Helper class for resolving users and groups in both local and domain contexts.
    /// </summary>
    internal static class Groups {
        private static string _hostname = string.Empty;

        /// <summary>
        /// Initializes the helper with the current machine's hostname.
        /// </summary>
        public static void Initialize() {
            _hostname = Environment.MachineName.ToUpperInvariant();
        }

        /// <summary>
        /// Gets the cached hostname.
        /// </summary>
        public static string Hostname => _hostname;

        /// <summary>
        /// Determines if a principal name (user or group) refers to a local principal.
        /// </summary>
        /// <param name="principalName">The principal name, optionally in DOMAIN\Name format</param>
        /// <returns>True if the principal is local, false otherwise</returns>
        public static bool IsLocalPrincipal(string principalName) {
            var parts = principalName.Split('\\');
            if (parts.Length == 2) {
                // Has domain prefix, compare with hostname
                string domain = parts[0].ToUpperInvariant();
                return string.Equals(domain, _hostname, StringComparison.OrdinalIgnoreCase);
            }
            // No domain prefix - assume it's a simple name that could be either local or domain
            // Default to domain context for backward compatibility
            return false;
        }

        /// <summary>
        /// Resolves a group name to its SID.
        /// </summary>
        /// <param name="groupName">The group name to resolve</param>
        /// <returns>Group resolution result containing SID and context information, or null if not found</returns>
        public static GroupResolutionResult ResolveGroup(string groupName) {
            if (string.IsNullOrWhiteSpace(groupName)) {
                return null;
            }

            bool isLocal = IsLocalPrincipal(groupName);
            ContextType contextType = isLocal ? ContextType.Machine : ContextType.Domain;

            try {
                using (PrincipalContext ctx = new PrincipalContext(contextType)) {
                    GroupPrincipal group = GroupPrincipal.FindByIdentity(ctx, groupName);
                    if (group != null) {
                        return new GroupResolutionResult {
                            GroupName = groupName,
                            Sid = group.Sid.Value,
                            IsLocal = isLocal,
                            ContextType = contextType
                        };
                    }
                }
            } catch (Exception ex) {
                return new GroupResolutionResult {
                    GroupName = groupName,
                    IsLocal = isLocal,
                    ContextType = contextType,
                    Error = ex.Message
                };
            }

            return null;
        }

        /// <summary>
        /// Gets the group membership SIDs for a user.
        /// </summary>
        /// <param name="userName">The username, optionally in DOMAIN\Username format</param>
        /// <returns>User resolution result containing group SIDs, or null if user not found</returns>
        public static UserResolutionResult ResolveUserGroups(string userName) {
            if (string.IsNullOrWhiteSpace(userName)) {
                return null;
            }

            string samAccountName = userName;
            string domain = null;
            var parts = userName.Split('\\');
            if (parts.Length == 2) {
                domain = parts[0];
                samAccountName = parts[1];
            }

            // Determine if we should use local or domain context
            bool isLocal = (domain != null && string.Equals(domain, _hostname, StringComparison.OrdinalIgnoreCase));
            ContextType contextType = isLocal ? ContextType.Machine : ContextType.Domain;

            try {
                PrincipalContext ctx = isLocal
                    ? new PrincipalContext(ContextType.Machine)
                    : new PrincipalContext(ContextType.Domain, domain);

                using (ctx) {
                    UserPrincipal user = UserPrincipal.FindByIdentity(ctx, samAccountName);
                    if (user != null) {
                        var groupSids = new HashSet<string>();
                        var userGroups = user.GetAuthorizationGroups();
                        foreach (var group in userGroups) {
                            var sid = group.Sid?.Value;
                            if (sid != null) {
                                groupSids.Add(sid);
                            }
                        }

                        return new UserResolutionResult {
                            UserName = userName,
                            IsLocal = isLocal,
                            ContextType = contextType,
                            GroupSids = groupSids
                        };
                    }
                }
            } catch (Exception ex) {
                return new UserResolutionResult {
                    UserName = userName,
                    IsLocal = isLocal,
                    ContextType = contextType,
                    Error = ex.Message
                };
            }

            return null;
        }
    }

    /// <summary>
    /// Result of a group resolution operation.
    /// </summary>
    internal class GroupResolutionResult {
        public string GroupName { get; set; }
        public string Sid { get; set; }
        public bool IsLocal { get; set; }
        public ContextType ContextType { get; set; }
        public string Error { get; set; }

        public bool Success => !string.IsNullOrEmpty(Sid);
        public string ContextName => IsLocal ? "local" : "domain";
    }

    /// <summary>
    /// Result of a user resolution operation.
    /// </summary>
    internal class UserResolutionResult {
        public string UserName { get; set; }
        public bool IsLocal { get; set; }
        public ContextType ContextType { get; set; }
        public HashSet<string> GroupSids { get; set; }
        public string Error { get; set; }

        public bool Success => GroupSids != null && string.IsNullOrEmpty(Error);
        public string ContextName => IsLocal ? "local" : "domain";
    }
}
