using PlustekBCR.Models;

namespace PlustekBCR.Helpers
{
    public static class BusinessCardDepartmentHelper
    {
        public static int GetVisibleDepartmentCount(BusinessCard? card)
        {
            if (card == null)
            {
                return 1;
            }

            if (!string.IsNullOrWhiteSpace(card.Department4)) return 4;
            if (!string.IsNullOrWhiteSpace(card.Department3)) return 3;
            if (!string.IsNullOrWhiteSpace(card.Department2)) return 2;
            return 1;
        }
    }
}
