// using System;
// using System.ComponentModel.DataAnnotations;
// using System.ComponentModel.DataAnnotations.Schema;
//
// namespace TagReporter.Domains;
//
// public class WiredTag
// {
//     [Key]
//     [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
//     public Guid Id { get; set; }
//
//     public string Name { get; set; }
//     
//     public string MacAddress { get; set; }
//     
//     public WiredTag(string name, string macAddress)
//     {
//         Name = name;
//         MacAddress = macAddress;
//     }
// }