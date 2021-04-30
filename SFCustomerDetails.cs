using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Project.Models
{
	[StoredProcedureName("GetCustomerDetails")]
	public class SFCustomerDetails
	{
		[QueryParam]
		[Required]
		[DisplayName("Mobile No")]
		[RegularExpression("^[0-9]*$", ErrorMessage = "{0} should contains only numbers.")]
		public string MobileNo { get; set; }

		[Required]
		[DisplayName("Name")]
		public string Name { get; set; }

		//[QueryParam]
		[Required]
		[DisplayName("ID Type")]
		public string IdType { get; set; }

		//[QueryParam]
		[Required]
		[DisplayName("NIC/Passport No")]
		public string IdNumber { get; set; }

		[DisplayName("Customer Id")]
		[QueryParam(direction: Direction.Output)]
		public long? CustomerId { get; set; }
	}
}
