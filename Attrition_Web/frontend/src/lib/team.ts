import type { StaticImageData } from "next/image";

import binh from "@/content/dev-team/phan-phuc-binh.jpg";
import dangNN from "@/content/dev-team/nguyen-nhat-dang.jpg";
import dangTT from "@/content/dev-team/tran-thien-dang.jpg";
import hau from "@/content/dev-team/le-trung-hau.jpg";

/**
 * The four people who made Attrition, in credit order.
 */
export interface TeamMember {
  name: string;
  role: string;
  photo: StaticImageData;
}

export const TEAM: readonly TeamMember[] = [
  {
    name: "Phan Phuc Binh",
    role: "Project Lead · Creative Director",
    photo: binh,
  },
  {
    name: "Nguyen Nhat Dang",
    role: "Combat & Systems Programmer",
    photo: dangNN,
  },
  {
    name: "Le Trung Hau",
    role: "Narrative · UX/UI · Full-stack",
    photo: hau,
  },
  {
    name: "Tran Thien Dang",
    role: "Quality Assurance",
    photo: dangTT,
  },
];
