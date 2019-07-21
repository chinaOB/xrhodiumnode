﻿using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Text;
using System.Transactions;

namespace BRhodium.Bitcoin.Features.Wallet.Helpers
{
    internal class CreateDbStructureHelper
    {
        internal void CreateIt(SQLiteConnection connection)
        {
            using (var dbConnection = connection)
            {
                dbConnection.Open();

                using (var transaction = dbConnection.BeginTransaction())
                {
                    try
                    {
                        var sql = "CREATE TABLE \"Wallet\" (" +
                                "\"Id\"    INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, " +
                                "\"Name\"  TEXT NOT NULL UNIQUE, " +
                                "\"EncryptedSeed\" TEXT NOT NULL, " +
                                "\"ChainCode\" TEXT NOT NULL, " +
                                "\"Network\"   TEXT NOT NULL, " +
                                "\"CreationTime\"  INTEGER NOT NULL, " +
                                "\"LastBlockSyncedHash\" TEXT NULL, " +
                                "\"LastBlockSyncedHeight\" INTEGER NULL, " +
                                "\"CoinType\" INTEGER NOT NULL, " +
                                "\"LastUpdated\" INTEGER NOT NULL, " +
                                "\"Blocks\"    BLOB " +
                                "); ";
                        var command = new SQLiteCommand(sql, dbConnection);
                        command.ExecuteNonQuery();

                        sql = "CREATE TABLE \"Account\"(" +
                              "\"Id\"    INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT," +
                              "\"WalletId\"  INTEGER  NOT NULL," +
                              "\"HdIndex\"   INTEGER  NOT NULL," +
                              "\"Name\"  TEXT NOT NULL," +
                              "\"HdPath\"    TEXT NOT NULL," +
                              "\"ExtendedPubKey\"    TEXT NOT NULL," +
                              "\"CreationTime\"  INTEGER  NOT NULL" +
                        ");";
                        command = new SQLiteCommand(sql, dbConnection);
                        command.ExecuteNonQuery();

                        sql = "CREATE TABLE \"Address\"(" +
                              "\"Id\"    INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT," +
                              "\"WalletId\"  INTEGER NOT NULL," +
                              "\"HdIndex\"   INTEGER NOT NULL," +
                              "\"ScriptPubKey\"  TEXT NOT NULL," +
                              "\"Pubkey\"    TEXT NOT NULL," +
                              "\"ScriptPubKeyHash\" TEXT NOT NULL UNIQUE," +
                              "\"Address\"   TEXT NOT NULL UNIQUE," +
                              "\"HdPath\"    TEXT NOT NULL" +
                        ");";
                        command = new SQLiteCommand(sql, dbConnection);
                        command.ExecuteNonQuery();

                        sql = "CREATE TABLE \"Transaction\"(" +
                              "\"Id\"    INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT," +
                              "\"WalletId\"  INTEGER NOT NULL," +
                              "\"AddressId\" INTEGER NOT NULL," +
                              "\"TxIndex\"   INTEGER NOT NULL," +
                              "\"Hash\"  TEXT NOT NULL," +
                              "\"Amount\"    INTEGER NOT NULL," +
                              "\"BlockHeight\"   INTEGER NULL," +
                              "\"BlockHash\" TEXT NULL," +
                              "\"CreationTime\"  INTEGER NOT NULL," +
                              "\"MerkleProof\"   TEXT NULL," +
                              "\"ScriptPubKey\"  TEXT NOT NULL," +
                              "\"Hex\"   TEXT NULL," +
                              "\"IsPropagated\"  NUMERIC NOT NULL," +
                              "\"IsSpent\"   NUMERIC NOT NULL" +
                        ");";
                        command = new SQLiteCommand(sql, dbConnection);
                        command.ExecuteNonQuery();

                        sql = " CREATE TABLE \"TransactionSpendingLinks\"(" +
                              "\"WalletId\"  INTEGER NOT NULL," +
                              "\"TransactionId\" INTEGER NOT NULL," +
                              "\"SpendingTransactionId\" INTEGER NOT NULL," +
                              "PRIMARY KEY(WalletId, TransactionId, SpendingTransactionId)" +
                        ");";
                        command = new SQLiteCommand(sql, dbConnection);
                        command.ExecuteNonQuery();

                        sql = " CREATE TABLE \"SpendingDetails\"(" +
                            "\"Id\"    INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT," +
                            "\"WalletId\"  INTEGER NOT NULL," +
                            "\"TransactionHash\"   TEXT NOT NULL," +
                            "\"BlockHeight\"   INTEGER NULL," +
                            "\"CreationTime\"  INTEGER NOT NULL," +
                            "\"Hex\"   TEXT NULL" +
                        ");";

                        command = new SQLiteCommand(sql, dbConnection);
                        command.ExecuteNonQuery();

                        sql = "CREATE TABLE \"PaymentDetails\"( " +
                            "\"Id\"    INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, " +
                            "\"WalletId\"  INTEGER  NOT NULL,  " +
                            "\"SpendingTransactionId\" INTEGER  NOT NULL, " +
                            "\"Amount\"    INTEGER  NOT NULL," +
                            "\"DestinationAddress\"    TEXT NOT NULL," +
                            "\"DestinationScriptPubKey\"   TEXT NOT NULL" +
                        ");";
                        command = new SQLiteCommand(sql, dbConnection);
                        command.ExecuteNonQuery();

                        sql = "CREATE UNIQUE INDEX \"ix_Address\" ON \"Address\" (\"Address\");";
                        command = new SQLiteCommand(sql, dbConnection);
                        command.ExecuteNonQuery();

                        sql = "CREATE UNIQUE INDEX \"ix_Address_ScriptPubKey\" ON \"Address\" (\"ScriptPubKey\");";
                        command = new SQLiteCommand(sql, dbConnection);
                        command.ExecuteNonQuery();

                        sql = "CREATE UNIQUE INDEX \"ix_WalletName\" ON \"Wallet\" (\"Name\");";
                        command = new SQLiteCommand(sql, dbConnection);
                        command.ExecuteNonQuery();

                        sql = "CREATE INDEX \"ix_Transaction_WalletId\" ON \"Transaction\" (\"WalletId\");";
                        command = new SQLiteCommand(sql, dbConnection);
                        command.ExecuteNonQuery();

                        sql = "CREATE INDEX \"ix_SpendingDetails_WalletId\" ON \"SpendingDetails\" (\"WalletId\");";
                        command = new SQLiteCommand(sql, dbConnection);
                        command.ExecuteNonQuery();

                        transaction.Commit();
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }
    }
}
