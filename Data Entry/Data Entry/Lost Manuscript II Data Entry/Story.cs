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
        private Feature anchor_node;
        public int turn;

        public Story(Feature anchor_node_in)
        {
            turn = 0;
            anchor_node = anchor_node_in;
            story_sequence = new List<StoryNode>();
        }//end method Story

        public void AddStoryNode(StoryNode new_story_node)
        {
            story_sequence.Add(new_story_node);
            turn += 1;
        }//end method AddStoryNode

        //Get the story as a history list of features
        public List<Feature> GetHistory()
        {
            List<Feature> return_list = new List<Feature>();

            foreach (StoryNode temp_node in story_sequence)
            {
                return_list.Add(temp_node.graph_node);
            }//end foreach

            return return_list;
        }//end method GetHistory

        public Feature AnchorNode
        {
            get
            {
                return this.anchor_node;
            }//end get
            set
            {
                this.anchor_node = value;
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
