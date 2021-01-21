namespace ExchangeRates.Migrations

open FluentMigrator

[<Migration(0L, "Initial")>]
type InitialMigration() = 
    inherit Migration()
    
    override me.Up() = 
        me.Create.Table("ExchangeRates")
            .WithColumn("Id").AsInt32().NotNullable().PrimaryKey().Identity()
            .WithColumn("CurrencyCode").AsFixedLengthString(3).NotNullable()
            .WithColumn("Date").AsDate().NotNullable()
            .WithColumn("Rate").AsDecimal().NotNullable() |> ignore
        me.Create.UniqueConstraint("UK_ExchangeRates_CurrencyCode_Date")
            .OnTable("ExchangeRates").Columns("CurrencyCode", "Date") |> ignore

    override me.Down() =
        me.Delete.Table("ExchangeRates") |> ignore
