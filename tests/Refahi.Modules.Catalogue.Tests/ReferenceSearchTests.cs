using Refahi.Modules.References.Application;
using Refahi.Modules.References.Application.Contracts.Queries;
using Xunit;
namespace Refahi.Modules.Catalogue.Tests;
public sealed class ReferenceSearchTests
{
 [Theory]
 [InlineData("تهران", "Tehran", "ته", true)]
 [InlineData("کیش", "Kish", "كيش", true)]
 [InlineData("تهران", "Tehran", "TEHR", true)]
 [InlineData("تهران", "Tehran", "", true)]
 [InlineData("تهران", "Tehran", "شیراز", false)]
 public void QueriesMatchPersianVariantsEnglishAndEmptySearch(string name,string english,string query,bool expected) => Assert.Equal(expected,ReferenceSearch.Matches(name,english,query));
 [Fact] public void SearchLengthIsValidated(){Assert.False(new Refahi.Modules.References.Application.Features.Provinces.GetProvinces.GetProvincesQueryValidator().Validate(new GetProvincesQuery(Q:new string('a',101))).IsValid);Assert.False(new Refahi.Modules.References.Application.Features.Cities.GetCities.GetCitiesQueryValidator().Validate(new GetCitiesQuery(Q:new string('a',101))).IsValid);}
}
