using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Refahi.Modules.Hotels.Infrastructure.Persistence;
using System;

#nullable disable

namespace Refahi.Modules.Hotels.Infrastructure.Migrations
{
    [DbContext(typeof(HotelsDbContext))]
    [Migration("20260629093000_Hotels-AddProviderCancellationState")]
    public partial class HotelsAddProviderCancellationState : Migration
    {

    }
}
