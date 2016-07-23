using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dialogue_Data_Entry
{
    class StoryNode
    {
        //Which node in the feature graph this story node is presenting
        public Feature graph_node;
        //A list of tuples representing story acts. First item is the name of the act,
        //second item is the id of the target of the act (relative to the current node).
        public List<Tuple<string, int>> story_acts;

        public FeatureGraph graph;

        public StoryNode(Feature graph_node_in)
        {
            graph_node = graph_node_in;
        }//end constructor StoryNode

        public void AddStoryAct(string act_name, int target_id)
        {
            story_acts.Add(new Tuple<string, int>(act_name, target_id));
        }//end method AddStoryAct
    }
}
