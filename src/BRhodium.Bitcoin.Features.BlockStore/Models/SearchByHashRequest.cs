﻿using System.ComponentModel.DataAnnotations;

namespace BRhodium.Bitcoin.Features.BlockStore.Models
{
    public abstract class RequestBase
    {
        public bool OutputJson { get; set; }
    }

    public class SearchByHashRequest : RequestBase
    {
        [Required(AllowEmptyStrings = false)]
        public string Hash { get; set; }
    }
}
