using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dialogue_Data_Entry
{
    class Story
    {
        //A story is a list of story nodes.
        private List<StoryNode> story_sequence;
        private int anchor_node_id;
        public int current_turn;

        public Story(int anchor_node_id_in)
        {
            current_turn = 0;
            anchor_node_id = anchor_node_id_in;
            story_sequence = new List<StoryNode>();
        }//end method Story

        public void AddStoryNode(StoryNode new_story_node)
        {
            new_story_node.turn = current_turn;
            story_sequence.Add(new_story_node);
            current_turn += 1;
        }//end method AddStoryNode

        //Get the story as a history list of feature ids
        public List<int> GetHistory()
        {
            List<int> return_list = new List<int>();

            foreach (StoryNode temp_node in story_sequence)
            {
                return_list.Add(temp_node.graph_node_id);
            }//end foreach

            return return_list;
        }//end method GetHistory

        public int AnchorNodeId
        {
            get
            {
                return this.anchor_node_id;
            }//end get
            set
            {
                this.anchor_node_id = value;
            }//end set
        }
        public List<StoryNode> StorySequence
        {
            get
            {
                return this.story_sequence;
            }//end get
        }
    }
}
