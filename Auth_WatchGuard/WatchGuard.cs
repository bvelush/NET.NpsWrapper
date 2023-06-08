// --------------------------------------------------------------------------------------------------------------------
// <copyright>
//   Copyright (C) lestoilfante 2023 (https://github.com/lestoilfante)
//   
//   GNU General Public License version 2.1 (GPLv2.1) 
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

// --------------------------------------------------------------------------------------------------------------------
// WatchGuard MFA Authentication API Model
// --------------------------------------------------------------------------------------------------------------------
namespace Auth_WatchGuard
{
    /// <summary>
    /// Api entrypoint token
    /// </summary>
    internal class WgToken : WgError
    {
        public string access_token { get; set; }
        public string token_type { get; set; }
        public int expires_in { get; set; }
        public string scope { get; set; }
    }

    /// <summary>
    /// User Authentication
    /// </summary>
    internal class WgClientInfoRequest
    {
        public string machineName { get; set; }
        public string osVersion { get; set; }
        public string domain { get; set; }
    }
    internal class WgAuthMfa
    {
        public string login { get; set; }
        public string password { get; set; }
        public string type { get; set; }
        public string originIpAddress { get; set; }
        public WgClientInfoRequest clientInfoRequest { get; set; }
    }
    internal class WgAuth
    {
        public string login { get; set; }
        public string password { get; set; }
        public string originIpAddress { get; set; }
    }
    internal class WgTransaction : WgError
    {
        public string transactionId { get; set; }
        public string command { get; set; }
        public string authenticationResult { get; set; }
    }
    internal class WgTransactionResult : WgError
    {
        public string authenticationResult { get; set; }
        public string pushResult { get; set; }
    }

    /// <summary>
    /// Error Response
    /// </summary>
    internal class WgError
    {
        public string type { get; set; }
        public string title { get; set; }
        public int status { get; set; }
        public string detail { get; set; }
        public string instance { get; set; }
    }

}
