using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dialogue_Data_Entry
{
    class GraphNodeLight
    {
        public int id;
        public string entity_type;

        public GraphNodeLight()
        {
            id = -1;
            entity_type = "";
        }//end constructor GraphNodeLight
        public GraphNodeLight(Feature base_node)
        {
            id = base_node.Id;
            if (base_node.entity_type.Count > 0)
                entity_type = base_node.entity_type[0];
            else
                entity_type = "";
        }//end constructor GraphNodeLight
    }
}
