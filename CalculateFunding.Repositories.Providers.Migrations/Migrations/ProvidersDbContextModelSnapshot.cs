﻿// <auto-generated />
using CalculateFunding.Repositories.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Internal;
using System;

namespace CalculateFunding.Repositories.Providers.Migrations.Migrations
{
    [DbContext(typeof(ProvidersDbContext))]
    partial class ProvidersDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "2.0.1-rtm-125")
                .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

            modelBuilder.Entity("CalculateFunding.Repositories.Providers.ProviderCandidateEntity", b =>
                {
                    b.Property<long>("ProviderCommandId");

                    b.Property<string>("UKPRN");

                    b.Property<string>("Address3");

                    b.Property<string>("AdministrativeWard");

                    b.Property<string>("AdmissionsPolicy");

                    b.Property<string>("Authority");

                    b.Property<string>("Boarders");

                    b.Property<string>("CCF");

                    b.Property<string>("CensusAreaStatisticWard");

                    b.Property<DateTimeOffset?>("CensusDate");

                    b.Property<DateTimeOffset?>("CloseDate");

                    b.Property<string>("Country");

                    b.Property<string>("County");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .ValueGeneratedOnAdd();

                    b.Property<bool>("Deleted");

                    b.Property<string>("Diocese");

                    b.Property<string>("DistrictAdministrative");

                    b.Property<string>("EBD");

                    b.Property<int?>("Easting");

                    b.Property<string>("EdByOther");

                    b.Property<string>("EstablishmentName");

                    b.Property<string>("EstablishmentNumber");

                    b.Property<string>("EstablishmentStatus");

                    b.Property<string>("EstablishmentType");

                    b.Property<string>("EstablishmentTypeGroup");

                    b.Property<string>("FEHEIdentifier");

                    b.Property<string>("FTProv");

                    b.Property<string>("FederationFlag");

                    b.Property<string>("Federations");

                    b.Property<string>("FurtherEducationType");

                    b.Property<string>("GOR");

                    b.Property<string>("GSSLACode");

                    b.Property<string>("Gender");

                    b.Property<string>("LSOA");

                    b.Property<DateTimeOffset?>("LastChangedDate");

                    b.Property<string>("Locality");

                    b.Property<string>("MSOA");

                    b.Property<string>("Name");

                    b.Property<int?>("Northing");

                    b.Property<int?>("NumberOfBoys");

                    b.Property<int?>("NumberOfGirls");

                    b.Property<int?>("NumberOfPupils");

                    b.Property<string>("NurseryProvision");

                    b.Property<string>("OfficialSixthForm");

                    b.Property<DateTimeOffset?>("OfstedLastInspectionDate");

                    b.Property<string>("OfstedRating");

                    b.Property<string>("OfstedSpecialMeasures");

                    b.Property<DateTimeOffset?>("OpenDate");

                    b.Property<int?>("PRUPlaces");

                    b.Property<string>("ParliamentaryConstituency");

                    b.Property<decimal?>("PercentageFSM");

                    b.Property<string>("PhaseOfEducation");

                    b.Property<string>("Postcode");

                    b.Property<string>("RSCRegion");

                    b.Property<string>("ReasonEstablishmentClosed");

                    b.Property<string>("ReasonEstablishmentOpened");

                    b.Property<string>("ReligiousCharacter");

                    b.Property<string>("ReligiousEthos");

                    b.Property<int?>("ResourcedProvisionCapacity");

                    b.Property<int?>("ResourcedProvisionOnRoll");

                    b.Property<string>("SEN1");

                    b.Property<int?>("SENNoStat");

                    b.Property<string>("SENPRU");

                    b.Property<int?>("SENStat");

                    b.Property<int?>("SchoolCapacity");

                    b.Property<string>("SchoolSponsorFlag");

                    b.Property<string>("SchoolSponsors");

                    b.Property<string>("Section41Approved");

                    b.Property<int?>("SenUnitCapacity");

                    b.Property<int?>("SenUnitOnRoll");

                    b.Property<string>("SpecialClasses");

                    b.Property<int?>("StatutoryHighAge");

                    b.Property<int?>("StatutoryLowAge");

                    b.Property<string>("Street");

                    b.Property<string>("TeenMoth");

                    b.Property<int?>("TeenMothPlaces");

                    b.Property<string>("Telephone");

                    b.Property<byte[]>("Timestamp")
                        .IsConcurrencyToken()
                        .ValueGeneratedOnAddOrUpdate();

                    b.Property<string>("Town");

                    b.Property<string>("TrustSchoolFlag");

                    b.Property<string>("Trusts");

                    b.Property<string>("TypeOfResourcedProvision");

                    b.Property<string>("URN")
                        .IsRequired();

                    b.Property<DateTimeOffset>("UpdatedAt")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("UrbanRural");

                    b.Property<string>("Website");

                    b.HasKey("ProviderCommandId", "UKPRN");

                    b.HasIndex("UKPRN");

                    b.HasIndex("ProviderCommandId", "UKPRN");

                    b.ToTable("ProviderCandidates");
                });

            modelBuilder.Entity("CalculateFunding.Repositories.Providers.ProviderCommandEntity", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<DateTimeOffset>("CreatedAt")
                        .ValueGeneratedOnAdd();

                    b.Property<bool>("Deleted");

                    b.Property<string>("ProviderUKPRN");

                    b.Property<byte[]>("Timestamp")
                        .IsConcurrencyToken()
                        .ValueGeneratedOnAddOrUpdate();

                    b.Property<DateTimeOffset>("UpdatedAt")
                        .ValueGeneratedOnAdd();

                    b.HasKey("Id");

                    b.HasIndex("ProviderUKPRN");

                    b.ToTable("ProviderCommands");
                });

            modelBuilder.Entity("CalculateFunding.Repositories.Providers.ProviderEntity", b =>
                {
                    b.Property<string>("UKPRN")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("Address3");

                    b.Property<string>("AdministrativeWard");

                    b.Property<string>("AdmissionsPolicy");

                    b.Property<string>("Authority");

                    b.Property<string>("Boarders");

                    b.Property<string>("CCF");

                    b.Property<string>("CensusAreaStatisticWard");

                    b.Property<DateTimeOffset?>("CensusDate");

                    b.Property<DateTimeOffset?>("CloseDate");

                    b.Property<string>("Country");

                    b.Property<string>("County");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .ValueGeneratedOnAdd();

                    b.Property<bool>("Deleted");

                    b.Property<string>("Diocese");

                    b.Property<string>("DistrictAdministrative");

                    b.Property<string>("EBD");

                    b.Property<int?>("Easting");

                    b.Property<string>("EdByOther");

                    b.Property<string>("EstablishmentName");

                    b.Property<string>("EstablishmentNumber");

                    b.Property<string>("EstablishmentStatus");

                    b.Property<string>("EstablishmentType");

                    b.Property<string>("EstablishmentTypeGroup");

                    b.Property<string>("FEHEIdentifier");

                    b.Property<string>("FTProv");

                    b.Property<string>("FederationFlag");

                    b.Property<string>("Federations");

                    b.Property<string>("FurtherEducationType");

                    b.Property<string>("GOR");

                    b.Property<string>("GSSLACode");

                    b.Property<string>("Gender");

                    b.Property<string>("LSOA");

                    b.Property<DateTimeOffset?>("LastChangedDate");

                    b.Property<string>("Locality");

                    b.Property<string>("MSOA");

                    b.Property<string>("Name");

                    b.Property<int?>("Northing");

                    b.Property<int?>("NumberOfBoys");

                    b.Property<int?>("NumberOfGirls");

                    b.Property<int?>("NumberOfPupils");

                    b.Property<string>("NurseryProvision");

                    b.Property<string>("OfficialSixthForm");

                    b.Property<DateTimeOffset?>("OfstedLastInspectionDate");

                    b.Property<string>("OfstedRating");

                    b.Property<string>("OfstedSpecialMeasures");

                    b.Property<DateTimeOffset?>("OpenDate");

                    b.Property<int?>("PRUPlaces");

                    b.Property<string>("ParliamentaryConstituency");

                    b.Property<decimal?>("PercentageFSM");

                    b.Property<string>("PhaseOfEducation");

                    b.Property<string>("Postcode");

                    b.Property<string>("RSCRegion");

                    b.Property<string>("ReasonEstablishmentClosed");

                    b.Property<string>("ReasonEstablishmentOpened");

                    b.Property<string>("ReligiousCharacter");

                    b.Property<string>("ReligiousEthos");

                    b.Property<int?>("ResourcedProvisionCapacity");

                    b.Property<int?>("ResourcedProvisionOnRoll");

                    b.Property<string>("SEN1");

                    b.Property<int?>("SENNoStat");

                    b.Property<string>("SENPRU");

                    b.Property<int?>("SENStat");

                    b.Property<int?>("SchoolCapacity");

                    b.Property<string>("SchoolSponsorFlag");

                    b.Property<string>("SchoolSponsors");

                    b.Property<string>("Section41Approved");

                    b.Property<int?>("SenUnitCapacity");

                    b.Property<int?>("SenUnitOnRoll");

                    b.Property<string>("SpecialClasses");

                    b.Property<int?>("StatutoryHighAge");

                    b.Property<int?>("StatutoryLowAge");

                    b.Property<string>("Street");

                    b.Property<string>("TeenMoth");

                    b.Property<int?>("TeenMothPlaces");

                    b.Property<string>("Telephone");

                    b.Property<byte[]>("Timestamp")
                        .IsConcurrencyToken()
                        .ValueGeneratedOnAddOrUpdate();

                    b.Property<string>("Town");

                    b.Property<string>("TrustSchoolFlag");

                    b.Property<string>("Trusts");

                    b.Property<string>("TypeOfResourcedProvision");

                    b.Property<string>("URN")
                        .IsRequired();

                    b.Property<DateTimeOffset>("UpdatedAt")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("UrbanRural");

                    b.Property<string>("Website");

                    b.HasKey("UKPRN");

                    b.HasIndex("UKPRN");

                    b.ToTable("Providers");
                });

            modelBuilder.Entity("CalculateFunding.Repositories.Providers.ProviderEventEntity", b =>
                {
                    b.Property<long>("ProviderCommandId");

                    b.Property<string>("UKPRN");

                    b.Property<string>("Action")
                        .IsRequired();

                    b.Property<string>("Address3");

                    b.Property<string>("AdministrativeWard");

                    b.Property<string>("AdmissionsPolicy");

                    b.Property<string>("Authority");

                    b.Property<string>("Boarders");

                    b.Property<string>("CCF");

                    b.Property<string>("CensusAreaStatisticWard");

                    b.Property<DateTimeOffset?>("CensusDate");

                    b.Property<DateTimeOffset?>("CloseDate");

                    b.Property<string>("Country");

                    b.Property<string>("County");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .ValueGeneratedOnAdd();

                    b.Property<bool>("Deleted");

                    b.Property<string>("Diocese");

                    b.Property<string>("DistrictAdministrative");

                    b.Property<string>("EBD");

                    b.Property<int?>("Easting");

                    b.Property<string>("EdByOther");

                    b.Property<string>("EstablishmentName");

                    b.Property<string>("EstablishmentNumber");

                    b.Property<string>("EstablishmentStatus");

                    b.Property<string>("EstablishmentType");

                    b.Property<string>("EstablishmentTypeGroup");

                    b.Property<string>("FEHEIdentifier");

                    b.Property<string>("FTProv");

                    b.Property<string>("FederationFlag");

                    b.Property<string>("Federations");

                    b.Property<string>("FurtherEducationType");

                    b.Property<string>("GOR");

                    b.Property<string>("GSSLACode");

                    b.Property<string>("Gender");

                    b.Property<string>("LSOA");

                    b.Property<DateTimeOffset?>("LastChangedDate");

                    b.Property<string>("Locality");

                    b.Property<string>("MSOA");

                    b.Property<string>("Name");

                    b.Property<int?>("Northing");

                    b.Property<int?>("NumberOfBoys");

                    b.Property<int?>("NumberOfGirls");

                    b.Property<int?>("NumberOfPupils");

                    b.Property<string>("NurseryProvision");

                    b.Property<string>("OfficialSixthForm");

                    b.Property<DateTimeOffset?>("OfstedLastInspectionDate");

                    b.Property<string>("OfstedRating");

                    b.Property<string>("OfstedSpecialMeasures");

                    b.Property<DateTimeOffset?>("OpenDate");

                    b.Property<int?>("PRUPlaces");

                    b.Property<string>("ParliamentaryConstituency");

                    b.Property<decimal?>("PercentageFSM");

                    b.Property<string>("PhaseOfEducation");

                    b.Property<string>("Postcode");

                    b.Property<string>("RSCRegion");

                    b.Property<string>("ReasonEstablishmentClosed");

                    b.Property<string>("ReasonEstablishmentOpened");

                    b.Property<string>("ReligiousCharacter");

                    b.Property<string>("ReligiousEthos");

                    b.Property<int?>("ResourcedProvisionCapacity");

                    b.Property<int?>("ResourcedProvisionOnRoll");

                    b.Property<string>("SEN1");

                    b.Property<int?>("SENNoStat");

                    b.Property<string>("SENPRU");

                    b.Property<int?>("SENStat");

                    b.Property<int?>("SchoolCapacity");

                    b.Property<string>("SchoolSponsorFlag");

                    b.Property<string>("SchoolSponsors");

                    b.Property<string>("Section41Approved");

                    b.Property<int?>("SenUnitCapacity");

                    b.Property<int?>("SenUnitOnRoll");

                    b.Property<string>("SpecialClasses");

                    b.Property<int?>("StatutoryHighAge");

                    b.Property<int?>("StatutoryLowAge");

                    b.Property<string>("Street");

                    b.Property<string>("TeenMoth");

                    b.Property<int?>("TeenMothPlaces");

                    b.Property<string>("Telephone");

                    b.Property<byte[]>("Timestamp")
                        .IsConcurrencyToken()
                        .ValueGeneratedOnAddOrUpdate();

                    b.Property<string>("Town");

                    b.Property<string>("TrustSchoolFlag");

                    b.Property<string>("Trusts");

                    b.Property<string>("TypeOfResourcedProvision");

                    b.Property<string>("URN")
                        .IsRequired();

                    b.Property<DateTimeOffset>("UpdatedAt")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("UrbanRural");

                    b.Property<string>("Website");

                    b.HasKey("ProviderCommandId", "UKPRN");

                    b.HasIndex("UKPRN");

                    b.HasIndex("ProviderCommandId", "UKPRN");

                    b.ToTable("ProviderEvents");
                });

            modelBuilder.Entity("CalculateFunding.Repositories.Providers.ProviderCandidateEntity", b =>
                {
                    b.HasOne("CalculateFunding.Repositories.Providers.ProviderCommandEntity", "ProviderCommand")
                        .WithMany()
                        .HasForeignKey("ProviderCommandId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.HasOne("CalculateFunding.Repositories.Providers.ProviderEntity", "Provider")
                        .WithMany()
                        .HasForeignKey("UKPRN")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("CalculateFunding.Repositories.Providers.ProviderCommandEntity", b =>
                {
                    b.HasOne("CalculateFunding.Repositories.Providers.ProviderEntity", "Provider")
                        .WithMany()
                        .HasForeignKey("ProviderUKPRN");
                });

            modelBuilder.Entity("CalculateFunding.Repositories.Providers.ProviderEventEntity", b =>
                {
                    b.HasOne("CalculateFunding.Repositories.Providers.ProviderCommandEntity", "ProviderCommand")
                        .WithMany()
                        .HasForeignKey("ProviderCommandId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.HasOne("CalculateFunding.Repositories.Providers.ProviderEntity", "Provider")
                        .WithMany()
                        .HasForeignKey("UKPRN")
                        .OnDelete(DeleteBehavior.Cascade);
                });
#pragma warning restore 612, 618
        }
    }
}
