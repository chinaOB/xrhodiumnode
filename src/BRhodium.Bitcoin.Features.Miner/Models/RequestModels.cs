﻿using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace BRhodium.Bitcoin.Features.Miner.Models
{
    /// <summary>
    /// Base model for requests.
    /// </summary>
    public class RequestModel
    {
        /// <summary>
        /// Creates a JSON serialized object.
        /// </summary>
        /// <returns>A JSON serialized object.</returns>
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }

    /// <summary>
    /// Model for the "startstaking" request.
    /// </summary>
    public class StartStakingRequest : RequestModel
    {
        /// <summary>
        /// The wallet password.
        /// </summary>
        [Required(ErrorMessage = "A password is required.")]
        public string Password { get; set; }

        /// <summary>
        /// The wallet name.
        /// </summary>
        [Required(ErrorMessage = "The name of the wallet is missing.")]
        public string Name { get; set; }
    }
}
