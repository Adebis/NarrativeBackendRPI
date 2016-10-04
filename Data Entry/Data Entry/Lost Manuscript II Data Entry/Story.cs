using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dialogue_Data_Entry
{
    class Story
    {
        //A story is a list of story nodes.
        private List<StorySegment> story_sequence;
        public int current_turn;
        public int last_anchor_id;
        public int last_segment_turn;

        public Story()
        {
            current_turn = 0;
            last_anchor_id = 0;
            story_sequence = new List<StorySegment>();
        }//end method Story
        public Story(StorySegment base_story_segment)
        {
            current_turn = 0;
            story_sequence = new List<StorySegment>();
            last_anchor_id = base_story_segment.anchor_node_id;

            story_sequence.Add(DeepClone.DeepCopy<StorySegment>(base_story_segment));
            current_turn += base_story_segment.length;
        }//end method Story
        public Story(List<StorySegment> base_story_sequence)
        {
            current_turn = 0;
            story_sequence = new List<StorySegment>();
            last_anchor_id = base_story_sequence.Last<StorySegment>().anchor_node_id;

            foreach (StorySegment base_segment in base_story_sequence)
            {
                story_sequence.Add(DeepClone.DeepCopy<StorySegment>(base_segment));
                current_turn += base_segment.length;
            }//end foreach
        }//end method Story

        //Add a story node to the last arc in the sequence.
        public void AddStoryNode(StoryNode new_story_node)
        {
            if (story_sequence.Count > 0)
            {
                story_sequence.Last<StorySegment>().AddStoryNode(new_story_node);
                current_turn += 1;
            }//end if
            else
                BeginStorySegment(new_story_node);
        }//end method AddStoryNode

        public void BeginStorySegment(StoryNode anchor_node)
        {
            story_sequence.Add(new StorySegment(anchor_node));
            story_sequence.Last<StorySegment>().starting_turn = current_turn;
            current_turn += 1;
            last_segment_turn = story_sequence.Last<StorySegment>().starting_turn;
            last_anchor_id = anchor_node.graph_node_id;
        }//end method BeginStorySegment

        //Split the segment at the given story turn into two segments.
        //The point at which it splits becomes the end of the first segment.
        public void SplitSegment(int turn_in)
        {
            StorySegment to_split = null;
            //First, find the segment at the given turn.
            foreach (StorySegment temp_segment in story_sequence)
            {
                if (temp_segment.starting_turn <= turn_in)
                {
                    to_split = temp_segment;
                }//end if
            }//end foreach

            //Split the segment in half.
            List<StoryNode> first_half_sequence = to_split.Sequence.GetRange(0, turn_in - to_split.starting_turn + 1);
            StorySegment first_half = new StorySegment(first_half_sequence, to_split.starting_turn);
            List<StoryNode> second_half_sequence = to_split.Sequence.GetRange(turn_in + 1, to_split.length - first_half_sequence.Count);
            StorySegment second_half = new StorySegment(second_half_sequence, turn_in + 1);

            int to_split_index = story_sequence.IndexOf(to_split);
            //Remove segment we've split from the sequence
            story_sequence.RemoveAt(to_split_index);
            //Insert the first half at the index.
            story_sequence.Insert(to_split_index, first_half);
            //Insert the second half after the first.
            if (to_split_index + 1 >= story_sequence.Count)
                story_sequence.Add(second_half);
            else
                story_sequence.Insert(to_split_index + 1, second_half);
        }//end method SplitSegment

        public StoryNode GetLastNode()
        {
            if (story_sequence.Count > 0)
            {
                return story_sequence.Last<StorySegment>().GetSequence().Last<StoryNode>();
            }//end if
            else
                return null;
        }//end method GetLastNode

        public List<StoryNode> GetNodeSequence()
        {
            List<StoryNode> return_sequence = new List<StoryNode>();
            foreach (StorySegment temp_segment in story_sequence)
            {
                foreach (StoryNode temp_node in temp_segment.GetSequence())
                {
                    return_sequence.Add(temp_node);
                }//end foreach
            }//end foreach

            return return_sequence;
        }//end method GetNodeSequence

        //Get the story as a history list of feature ids
        public List<int> GetHistory()
        {
            List<int> return_list = new List<int>();

            foreach (StorySegment temp_segment in story_sequence)
            {
                return_list.AddRange(temp_segment.GetSequenceIds());
            }//end foreach

            return return_list;
        }//end method GetHistory

        //Return the last segment.
        public StorySegment GetLastSegment()
        {
            return story_sequence.Last<StorySegment>();
        }//end method GetLastSegment
        public List<StoryNode> GetRemainderStory()
        {
            List<StoryNode> remainder_story = new List<StoryNode>();
            for (int i = 0; i < story_sequence.Count - 1; i++)
            {
                for (int j = 0; j < story_sequence[i].length; j++)
                {
                    remainder_story.Add(story_sequence[i].GetSequence()[j]);
                }//end for
            }//end for
            return remainder_story;
        }//end method GetRemainderStory
        public StorySegment GetSecondToLastSegment()
        {
            if (StorySequence.Count >= 2)
            {
                return story_sequence[story_sequence.Count - 2];
            }//end if
            return null;
        }//end method GetSecondToLastSegment

        //Get the most recent location that we can find on a node in the story, as well as the id
        //of the node.
        public Tuple<double, double, int> GetLastLocation(FeatureGraph graph)
        {
            Tuple<double, double, int> last_location_info = null;
            StoryNode current_node = null;
            Feature current_feature = null;
            StorySegment current_segment = null;
            //Go through the list of nodes backwards, so the most recent
            //node is found first.
            for (int i = story_sequence.Count - 1; i >= 0; i--)
            {
                current_segment = story_sequence[i];
                for (int j = current_segment.length - 1; j >= 0; j--)
                {
                    current_node = current_segment.GetSequence()[j];
                    current_feature = graph.getFeature(current_node.graph_node_id);
                    if (current_feature.Geodata.Count > 0)
                    {
                        last_location_info = new Tuple<double, double, int>(current_feature.Geodata[0].Item1
                            , current_feature.Geodata[0].Item2
                            , current_feature.Id);
                        break;
                    }//end if
                }//end for
            }//end for

            return last_location_info;
        }//end method GetLastLocation

        /*public StoryNode GetNodeAtTurn(int turn)
        {
            StoryNode return_node = null;
            foreach (StorySegment temp_segment in StorySequence)
            {
                return_node = temp_segment.GetNodeAtTurn(turn);
                if (return_node != null)
                    return return_node;
            }//end foreach

            return return_node;
        }//end method GetNodeAtTurn*/
        public StoryNode GetNodeAtTurn(int turn)
        {
            return GetNodeSequence()[turn];
        }//end method GetNodeAtTurn

        public StoryNode GetNodeByGraphId(int graph_id)
        {
            foreach (StoryNode temp_node in GetNodeSequence())
            {
                if (temp_node.graph_node_id == graph_id)
                    return temp_node;
            }//end foreach
            return null;
        }//end method GetNodeByGraphId

        public List<StorySegment> StorySequence
        {
            get
            {
                return this.story_sequence;
            }//end get
        }
    }
}
