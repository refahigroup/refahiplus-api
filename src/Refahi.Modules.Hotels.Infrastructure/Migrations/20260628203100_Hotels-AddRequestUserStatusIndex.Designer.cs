using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Refahi.Modules.Hotels.Infrastructure.Persistence;

#nullable disable

namespace Refahi.Modules.Hotels.Infrastructure.Migrations
{
    [DbContext(typeof(HotelsDbContext))]
    [Migration("20260628203100_Hotels-AddRequestUserStatusIndex")]
    public partial class HotelsAddRequestUserStatusIndex : Migration
    {

    }
}
